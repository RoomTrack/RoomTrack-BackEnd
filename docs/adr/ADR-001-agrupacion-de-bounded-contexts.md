# ADR-001 — Incidencias dentro de `accommodations-service` y Pagos dentro de `bookings-service`

- **Estado:** Aceptada (25/11/2026). Revisable antes del despliegue en Azure (ver "Cuándo revisarla").
- **Contexto de la observación:** retroalimentación del Avance 4 (criterio 2, *Refactoring to Microservices*): de los
  7 bounded contexts de CON-01, 5 tienen servicio propio; Incidencias está dentro de Accommodations y Pagos y
  Facturación dentro de Bookings. Se pide extraerlos o justificar formalmente la agrupación.

## Contexto

CON-01 define 7 bounded contexts: Identidad, Habitaciones y Housekeeping, Incidencias y Mantenimiento, Estancias,
Pagos y Facturación, Notificaciones y Analítica. El backend los despliega como 5 APIs + 1 worker:

| Servicio | Bounded contexts (módulos en el código) |
|---|---|
| `identity-service` | Identidad (IAM, Audit, dispositivos push) |
| `accommodations-service` | Habitaciones y Housekeeping (Accommodations, Housekeeping, ShiftHandovers) **+ Incidencias y Mantenimiento** (Incidents, PreventiveMaintenance) + GuestRequests, Messaging, Media, IoT |
| `bookings-service` | Estancias (Bookings, check-in/check-out) **+ Pagos y Facturación** (Payments: pago de reserva, folio, comprobantes) |
| `profiles-service` | Perfiles de huésped y personal, turnos (Shifts), Marketing |
| `analytics-service` | Analítica (read model) |
| `notifications-worker` | Notificaciones (entrega de correo y push) |

Cada módulo mantiene su propia estructura DDD (`Domain/Application/Infrastructure/Interfaces`), sus propias tablas y
se comunica con los demás solo por su fachada ACL (`I…ContextFacade`) o por eventos de dominio. Es decir, la frontera
del bounded context **existe en el código**; lo que se decide aquí es si también debe ser una frontera de **proceso y
base de datos**.

## Decisión

Mantener, por ahora, Incidencias en `accommodations-service` y Pagos y Facturación en `bookings-service`, como
**módulos aislados dentro de un servicio** (patrón *modular service*), porque ambos comparten una **invariante
transaccional** con el contexto que los aloja:

1. **Incidencias ↔ Habitaciones.** Una incidencia que bloquea la habitación (US-08) la pasa a `Maintenance` y, al
   resolverse la última incidencia bloqueante (US-09), la habitación vuelve a `Cleaning` y nace su tarea de limpieza
   (US-05). La regla "una habitación con una incidencia bloqueante abierta nunca está `Available`" se garantiza hoy
   en **una sola transacción** (`IncidentCommandService` → `IRoomCommandService`). Separarlas obligaría a una saga con
   compensación y a una ventana en la que recepción podría asignar una habitación averiada a un huésped.
2. **Pagos ↔ Estancias.** El pago confirma la reserva (decisión D1) y el folio impide el check-out con saldo pendiente
   (US-14/16). Ambas reglas se cumplen hoy en **una sola transacción**: el pago y la confirmación se guardan juntos, y
   el check-out consulta el folio por el puerto `IStayFolioGate` antes de cerrar la estancia. Separarlas expone a
   pagos sin reserva confirmada (o reservas confirmadas sin pago) ante una falla parcial.

En ambos casos la alternativa distribuida (saga + outbox + compensaciones + idempotencia) añade complejidad y puntos de
falla sin un beneficio de escalabilidad que hoy se necesite: Incidencias y Pagos tienen una carga muy inferior a la de
sus servicios anfitriones y el mismo ciclo de despliegue.

## Consecuencias

- **Positivas:** consistencia fuerte en las dos invariantes más sensibles del negocio; menos servicios que operar en el
  plan gratuito de Render; las pruebas de aceptación (Reqnroll) ejercitan el flujo completo en proceso.
- **Negativas:** `accommodations-service` concentra varios módulos (mayor tamaño de despliegue); un fallo de ese servicio
  afecta a habitaciones e incidencias a la vez; no se pueden escalar por separado.
- **Mitigaciones ya aplicadas:**
  - Cada módulo tiene su propio esquema de tablas, su `ModelBuilderExtensions` y su registro DI
    (`Add…ContextServices`), por lo que extraerlo es mover carpetas + cambiar la fachada ACL local por un cliente HTTP
    (`HttpRoomReservationsFacade`, `HttpAccommodationsContextFacade` ya muestran el patrón).
  - La comunicación entre módulos ya pasa por fachadas ACL y eventos de dominio, nunca por repositorios ajenos.
  - El documento OpenAPI de cada servicio describe solo sus contextos (`ServiceDescriptions`).

## Cuándo revisarla (criterios de extracción)

Se extraerá `incidents-service` o `billing-service` cuando se cumpla **alguno** de estos criterios:

1. Se integre una pasarela de pago real o la facturación electrónica SUNAT (OSE/PSE): Pagos pasará a tener un ritmo de
   cambio, dependencias externas y requisitos de cumplimiento (PCI) propios → `billing-service`.
2. Mantenimiento se integre con sensores IoT o un CMMS externo, o su volumen supere al de Habitaciones →
   `maintenance-service`.
3. Se migre a Azure con despliegue independiente por equipo (ver `docs/arquitectura-cloud-azure.md`).

El plan de extracción (strangler fig) sería: (1) publicar los eventos que hoy son internos como eventos de integración,
(2) reemplazar la llamada transaccional por una saga orquestada con compensación (`HoldRoomForIncident` ↔
`ReleaseRoomFromIncident`; `ConfirmBooking` ↔ `RefundPayment`), (3) mover las tablas a su propio esquema y
(4) enrutar `/api/v1/incidents`, `/api/v1/preventive-maintenance-plans` o `/api/v1/receipts` y `/bookings/{id}/folio` al
nuevo servicio en el gateway YARP, cada paso en su propio pull request con su prueba de integración.
