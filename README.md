# RoomTrack — Backend (microservicios)

API REST de RoomTrack (ASP.NET Core 9, MySQL 8, RabbitMQ) organizada como **microservicios**: un API gateway,
cinco servicios con su propia base de datos y un worker de notificaciones. Los clientes solo hablan con el gateway;
la documentación de todos los servicios está en `http://localhost:8080/docs`.

**Producción:** API en `https://roomtrack-api.onrender.com/api/v1/...` y Swagger UI de todos los servicios en
[https://roomtrack-api.onrender.com/docs](https://roomtrack-api.onrender.com/docs) (si los servicios están dormidos,
la primera carga tarda ~30–60 s).

## Arquitectura

```
                        clientes (web, landing, app)
                                    │  /api/v1/*
                            ┌───────▼────────┐
                            │  API gateway   │  YARP: enrutamiento, CORS, Swagger UI unificado
                            └───────┬────────┘
        ┌──────────────┬────────────┼─────────────┬───────────────┐
        ▼              ▼            ▼             ▼               ▼
   identity      accommodations  bookings      profiles       analytics
   IAM, Audit,   hoteles, hab.,  reservas,     huéspedes,     métricas
   disp. push    media, IoT,     check-in/out, staff, demo    (read model)
                 limpieza,       pagos         requests
                 incidencias,
                 solicitudes,
                 mensajería
        │              │            │             │               ▲
        └──── HTTP interno (/internal/v1, X-Internal-Key) ────────┘│
        │              │            │             │               │
        └──────────────┴──── RabbitMQ (MassTransit) ───────────────┘
            eventos de integración, SendEmail, notificaciones push
                                    │
                         ┌──────────▼───────────┐
                         │ notifications-worker │ → correo: Brevo / SMTP / log
                         └──────────────────────┘ → push: Firebase Cloud Messaging / log
```

| Servicio | Bounded contexts | Base de datos | Rutas públicas (`/api/v1/...`) |
|---|---|---|---|
| `identity-service` | IAM, Audit, Notifications (dispositivos y preferencias de push) | `roomtrack_identity` | `authentication`, `users` (incluye `users/me/push-devices` y `users/me/notification-preferences`), `audit-logs` |
| `accommodations-service` | Accommodations, Media, emulador IoT, Housekeeping, Incidents, GuestRequests, Messaging | `roomtrack_accommodations` | `hotels`, `rooms`, `room-types`, `accommodations/options`, `media`, `io-t-emulator`, `cleaning-tasks`, `incidents`, `guest-requests`, `conversations` |
| `bookings-service` | Bookings (con check-in y check-out), Payments | `roomtrack_bookings` | `bookings`, `payments`, `rooms/available` |
| `profiles-service` | Profiles, Marketing | `roomtrack_profiles` | `guests`, `staff`, `demo-requests` |
| `analytics-service` | Analytics | `roomtrack_analytics` | `analytics` |
| `notifications-worker` | entrega de correos y notificaciones push | — (solo RabbitMQ) | — |
| `gateway` | — | — | todas las anteriores + `/docs` y `/health` |

Lo que cubre cada contexto nuevo de `accommodations-service` (las historias de usuario se citan en el código):

| Contexto | Historias | Qué hace |
|---|---|---|
| Housekeeping | US-05, US-06 | Tareas de limpieza: se crean al pasar una habitación a `Cleaning`, se asignan (a mano o al personal con menos carga) y se siguen hasta terminar. |
| Incidents | US-08, US-09 | Incidencias de mantenimiento: pueden bloquear la habitación hasta resolverse; las críticas no atendidas a tiempo se escalan. |
| GuestRequests | US-11, US-19 | Solicitudes del huésped durante su estadía: recepción las deriva al área que las atiende y el huésped ve el estado y el tiempo estimado. |
| Messaging | US-12, US-20 | Conversaciones dentro de la plataforma: huésped con recepción, canal de cada área y comentarios sobre una tarea. |

Las notificaciones push (US-22) avisan de estos cambios a quien corresponde: el personal asignado, el área o el huésped.

### Estructura del repositorio

```
src/
  BuildingBlocks/
    RoomTrack.Contracts/       Lenguaje publicado: puertos ACL entre servicios y mensajes de RabbitMQ
    RoomTrack.BuildingBlocks/  Infraestructura común: DbContext base, unit of work, errores (ProblemDetails),
                               JWT y políticas, API interna + clientes HTTP, MassTransit, OpenAPI, rate limiting
  Services/<Servicio>/RoomTrack.<Servicio>.API/   un proyecto ASP.NET Core por servicio
  Services/Notifications/RoomTrack.Notifications.Worker/
  Gateway/RoomTrack.Gateway/
tests/RoomTrack.<Servicio>.API.Tests/
deploy/mysql/init/             crea un esquema y un usuario por servicio
```

Dentro de cada servicio el código mantiene las capas de DDD del monolito (`Domain`, `Application`,
`Infrastructure`, `Interfaces`) en el namespace de su bounded context (`RoomTrack.<Contexto>...`, p. ej.
`RoomTrack.Housekeeping.Domain.Model.Aggregates`). Lo propio de cada servicio está en el namespace
`RoomTrack.<Servicio>.API` (su `DbContext`, su API interna y su `Program.cs`).

Las tareas de limpieza, las incidencias y las solicitudes de huéspedes llevan una **versión de concurrencia**
(`IHasConcurrencyVersion`): si dos personas cambian la misma a la vez, la segunda recibe **409** en lugar de pisar
el cambio de la primera, y el cliente debe volver a cargarla.

### Cómo se comunican los servicios

**1. Autenticación sin ida y vuelta.** `identity-service` firma los JWT y **cada servicio los valida por su
cuenta** (misma clave, issuer y audience). Lo único que se consulta a Identity es si la sesión del token sigue
vigente (usuario activo, versión del token): `GET /internal/v1/sessions/{userId}/{version}`, con caché de 15 s
por usuario y versión. Las políticas por capacidad (`Policies`) son las mismas en todos los servicios.

**2. Consultas y comandos síncronos: HTTP interno.** Los *facades* ACL del monolito (`IIamContextFacade`,
`IAccommodationsContextFacade`, `IGuestProfilesContextFacade`, `IRoomReservationsFacade`) se mantienen como
puertos en `RoomTrack.Contracts`. El servicio dueño los implementa en proceso y los expone en `/internal/v1/*`;
los demás los implementan con un cliente HTTP (`RoomTrack.BuildingBlocks/Clients`), así que el código de
aplicación no cambió. Detalles:

- `/internal/*` **no lo enruta el gateway** y además exige la cabecera `X-Internal-Key` (`InternalApi__Key`, la
  misma en todos los servicios).
- Los errores remotos vuelven como la misma excepción de dominio (400/403/404/409/410 con su código estable), así
  que la respuesta al cliente es igual que en el monolito. Si el otro servicio no responde: **503**
  `service.unavailable`.
- Resiliencia: timeouts, circuit breaker y reintentos solo de métodos seguros (un `POST` nunca se repite).

**3. Hechos asíncronos: RabbitMQ con outbox transaccional.** Cada servicio publica mensajes con MassTransit usando
el **outbox de Entity Framework** de su propia base de datos: el mensaje se guarda en `outbox_messages` en la misma
transacción que el cambio, y se envía a RabbitMQ después. Si el cambio se revierte, el mensaje no existe; si se
confirma, llega aunque el servicio se reinicie (al menos una vez). Los consumidores usan el **inbox**, que descarta
los duplicados.

| Mensaje | Publica | Consume | Para qué |
|---|---|---|---|
| `SendEmail` | todos los servicios | notifications-worker | entregar el correo (Brevo/SMTP/log) |
| `RoomRegisteredIntegrationEvent`, `RoomRemovedIntegrationEvent` | accommodations | analytics | contar habitaciones por hotel |
| `BookingStateChangedIntegrationEvent` | bookings | analytics | reservas y cancelaciones del mes, ocupación |
| `PaymentStateChangedIntegrationEvent` | bookings | analytics | ingresos del mes |
| `SendPushNotification` | accommodations (cualquier servicio puede, con `IPushSender`) | identity | notificar a unos usuarios |
| `DeliverPushNotification` | identity | notifications-worker | entregar a los dispositivos (FCM/log) |
| `PushDeviceTokensRejected` | notifications-worker | identity | olvidar los dispositivos que FCM rechazó |

**Notificaciones push (US-22).** Un servicio solo sabe a qué *usuarios* avisar; los dispositivos viven en Identity.
Por eso la entrega tiene tres pasos: el servicio publica `SendPushNotification` con los ids de usuario y una
categoría (`PushCategories`: tarea asignada, cambio de prioridad, incidencia, escalamiento, solicitud de huésped,
mensaje); Identity descarta a quienes desactivaron esa categoría, busca sus dispositivos y publica
`DeliverPushNotification` con los tokens; el worker los entrega y, si FCM dice que un token ya no sirve (app
desinstalada o token vencido), publica `PushDeviceTokensRejected` para que Identity lo olvide. La app registra su
token en cada inicio de sesión con `POST /api/v1/users/me/push-devices`.

Los eventos de integración llevan el **estado actual** de la entidad (*event-carried state transfer*). Así
Analytics mantiene su propio *read model* (`room_facts`, `booking_facts`, `payment_facts`) sin consultar las tablas
de otros servicios. Cada fila guarda el instante del último hecho aplicado, de modo que un evento viejo que llegue
tarde nunca pisa un estado más nuevo. Las métricas son **eventualmente consistentes**.

### Qué cambió respecto del monolito (compromisos a conocer)

- **Sin transacciones entre servicios.** Donde el monolito hacía todo en una transacción, ahora la llamada
  remota va **al final**, justo antes del commit local, para que un rechazo remoto revierta lo local:
  - *Check-in* (bookings → accommodations): la habitación pasa a `Occupied` en Accommodations y después se
    confirma el check-in. Si falla el commit local después de ocupar la habitación (poco probable), la habitación
    queda ocupada: el siguiente paso natural sería una **saga** con compensación.
  - *Registro de hotel* (accommodations → identity): se guarda el hotel, Identity asigna el hotel al
    administrador y devuelve su nueva sesión. Si Identity falla, el hotel se revierte.
- **Bloqueo de habitación (R1).** El monolito bloqueaba la fila de la habitación; ahora esa fila vive en otra
  base de datos. Bookings serializa las reservas de una habitación con su propia tabla `room_booking_locks`
  (`SELECT ... FOR UPDATE` dentro de la transacción de la reserva).
- **Correos.** Ya no existe la tabla `outbox_emails` ni el job `POST /api/v1/emails/dispatch`: el outbox de
  MassTransit cumple esa función y el worker reintenta con backoff exponencial (`Email__Retry__*`). Después del
  último intento el mensaje queda en la cola `_error` de RabbitMQ.
- **Datos de demostración.** El seeder `DemoData` creaba cuentas, hoteles, reservas y pagos en una sola
  transacción sobre una sola base de datos, y eso ya no es posible. **Se retiró**: para recuperarlo habría que
  reescribirlo como un script que use la API pública a través del gateway.
- **Migración de datos.** Cada servicio parte con una migración `InitialSchema` nueva. Mover los datos de una base
  existente del monolito a las cinco bases es un paso aparte (no incluido).

## Ejecutar en local

### Docker Compose (todo el sistema)

```bash
cp .env.example .env   # opcional; sin .env se usan los valores de desarrollo
docker compose up -d --build
curl http://localhost:8080/health
```

- API: `http://localhost:8080/api/v1/...` (solo el gateway se publica en el host)
- Documentación de todos los servicios: `http://localhost:8080/docs`
- Consola de RabbitMQ: `http://localhost:15672` (`guest` / `guest`): colas, mensajes y la cola `_error`
- MySQL: `localhost:3306`. Cada servicio entra con su usuario (`identity_svc`, `bookings_svc`...), que solo ve su
  esquema. El script `deploy/mysql/init/01-databases.sql` corre solo la primera vez, con el volumen vacío
  (`docker compose down -v` para empezar de cero).
- Laboratorio de resiliencia de Analytics (Redis + ActiveMQ): `docker compose --profile lab up -d --build`,
  con `ANALYTICS_REDIS_CONNECTION=redis:6379` y `ANALYTICS_ACTIVEMQ_URI=tcp://activemq:61616` en `.env`.

### `dotnet run` (un servicio a la vez, para depurar)

Levanta solo la infraestructura con Compose y ejecuta los servicios desde el IDE o la terminal:

```bash
docker compose up -d mysql rabbitmq
dotnet run --project src/Services/Identity/RoomTrack.Identity.API          # http://localhost:5101
dotnet run --project src/Services/Accommodations/RoomTrack.Accommodations.API  # http://localhost:5102
dotnet run --project src/Services/Bookings/RoomTrack.Bookings.API          # http://localhost:5103
dotnet run --project src/Services/Profiles/RoomTrack.Profiles.API          # http://localhost:5104
dotnet run --project src/Services/Analytics/RoomTrack.Analytics.API        # http://localhost:5105
dotnet run --project src/Services/Notifications/RoomTrack.Notifications.Worker
dotnet run --project src/Gateway/RoomTrack.Gateway                         # http://localhost:5100
```

Los `appsettings.Development.json` ya traen las URLs entre servicios, las claves de desarrollo y la base de datos
de cada uno (usuario `roomtrack` / `roomtrack_dev`, creado por el script de inicialización). Cada servicio aplica
sus migraciones al arrancar. Para crear una migración nueva:

```bash
dotnet ef migrations add <Nombre> --project src/Services/Bookings/RoomTrack.Bookings.API \
  --output-dir Infrastructure/Persistence/Migrations
```

### Tests

```bash
dotnet test RoomTrack.sln
```

Hay un proyecto de tests por servicio y otro para el worker de notificaciones. La mayoría corre contra una base de
datos en memoria. Los tests de integración que necesitan bloqueos de fila y conexiones concurrentes usan un MySQL
real, que se indica en `TEST_MYSQL_CONNECTION`; si no hay uno disponible, esos tests se omiten.

**CI** (`.github/workflows/ci.yml`): en cada push y pull request a `develop` y `main` compila la solución y corre
todos los tests, con un MySQL 8 como servicio del job. Los resultados quedan como artefacto `test-results` del run.

## Configuración y secretos

Cada servicio lee su configuración por capas: `appsettings.json` (sin secretos), `appsettings.{Environment}.json`
y **variables de entorno** (`__` equivale a `:`; por ejemplo `TokenSettings__Secret` es `TokenSettings:Secret`).
Las opciones se validan al iniciar: si falta un valor obligatorio, el servicio no arranca y el log dice cuál falta.
`.env.example` lista las variables de Docker Compose.

Valores compartidos por todos los servicios (deben ser iguales en todos):

| Variable | Uso |
|---|---|
| `TokenSettings__Secret`, `__Issuer`, `__Audience` | Firma y validación de los JWT. |
| `InternalApi__Key` | Clave de las llamadas entre servicios (`X-Internal-Key`, mínimo 32 caracteres). |
| `Cron__ApiKey` | Clave del programador externo (`X-Cron-Key`). |
| `RabbitMq__Host`, `__Username`, `__Password` | Broker de mensajes. |
| `Services__<Servicio>__BaseUrl` | Dirección interna de los servicios que consume cada uno. |

**Correos** (solo en `notifications-worker`). `Email__Transport` elige el transporte de forma explícita:

- `BrevoApi` (producción): API HTTP transaccional de Brevo por el puerto 443. Requiere `Email__Brevo__ApiKey` (secreto) y `Email__From__Address` (remitente verificado en Brevo).
- `Smtp`: cualquier relay SMTP (`Email__Smtp__Host`, `Port`, `Username`, `Password`, `EnableSsl`), pensado para uso local (por ejemplo Mailpit).
- `Log`: el correo se escribe en el log con sus enlaces y códigos. Es el valor por defecto fuera de producción; en `Production` el worker no arranca con `Log` ni sin transporte.

Los rechazos permanentes (destinatario o remitente inválido) no se reintentan. En el log queda una línea por correo
con el destinatario enmascarado; nunca la API key ni el cuerpo.

**Notificaciones push** (solo en `notifications-worker`). `Push__Transport` elige el transporte:

- `Fcm` (producción): Firebase Cloud Messaging HTTP v1. Requiere `Push__Fcm__ServiceAccountJson` (secreto): la clave
  JSON de una cuenta de servicio del proyecto de Firebase con el rol *Firebase Cloud Messaging API Admin*, completa y
  en una sola línea. `Push__Fcm__ProjectId` es opcional (por defecto, el `project_id` de la clave). Si la clave no
  es válida, el worker no arranca.
- `Log` (por defecto): la notificación se escribe en el log. Es lo que usa Render mientras no haya un proyecto de
  Firebase configurado.

Los errores temporales de FCM (429, 5xx, timeouts) se reintentan; si FCM rechaza las credenciales (401/403), el
mensaje falla y el error queda en el log.

**Imágenes de hoteles** (`accommodations-service`). Se suben directo del navegador a Cloudinary con una firma de
corta vida (`POST /api/v1/media/hotel-images/signature`, solo administradores). Define `Cloudinary__CloudName`,
`Cloudinary__ApiKey` y `Cloudinary__ApiSecret` (obligatorias en producción).

**Medios de pago** de las reservas (Yape, Plin, cuenta bancaria): no son variables de entorno; cada administrador
los registra para su hotel desde la aplicación (`PUT /api/v1/hotels/{id}/payment-settings`).

**Tareas programadas.** `POST /api/v1/bookings/expire-pending`, `/rooms/maintenance-alerts`,
`/incidents/escalations` y `/demo-requests/follow-ups` se invocan a través del gateway con la cabecera `X-Cron-Key`
(`.github/workflows/scheduler.yml`).

**IP del cliente** (rate limiting y auditoría). El gateway agrega `X-Forwarded-For` y cada servicio lo procesa
saltando solo proxies de confianza: loopback, rangos privados (las redes de Docker incluidas) y los rangos de
Cloudflare. Así el cliente no puede falsear su IP. `ForwardedHeaders__TrustedNetworks__0`, `__1`... reemplazan la
lista por defecto.

**Servicios dormidos** (plan gratuito de Render, `render.yaml`). Cada servicio se duerme tras ~15 min sin tráfico, y
Render solo lo despierta con tráfico de fuera de Render: las llamadas del gateway no bastan. Por eso `/docs` despierta a
todos desde el navegador (un `fetch` `no-cors` al `/health` público de cada URL de `KeepAwake__Urls`, `KeepAwake.cs`), y
antes de reenviar a un servicio que no ha respondido en los últimos 10 minutos el gateway consulta su `/health` hasta
que responde 2xx, como mucho 90 s (`WakeBeforeProxy.cs`): la primera petición tras un rato sin uso tarda ~30–60 s en
lugar de fallar con 502 (en `/docs`, "Failed to load API definition"). Los clientes (front) deben hacer lo mismo al
cargar; si no, sus peticiones fallan con 502 hasta que algo de fuera despierte al servicio.
