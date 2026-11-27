# Accommodations — pruebas de aceptación (BDD)

Los `.feature` (Gherkin en español) se ejecutan con **Reqnroll** como tests de xUnit en `dotnet test` (y en la CI).

| Carpeta | Historias | Step definitions |
| --- | --- | --- |
| `Sprint1/` | US-05, US-06 (limpieza), US-07 (checklist), US-08, US-09 (incidencias) | `Steps/HousekeepingSteps.cs`, `Steps/IncidentSteps.cs`, `Steps/OperationsSteps.cs` |
| `Sprint3/` | US-10, US-30, US-11, US-19, US-20, US-12, US-32, US-23 | `Steps/OperationsSteps.cs` |
| `Sprint4/` | US-18 (panel del administrador), US-24 (productividad del personal), US-25 (tiempos de atención), US-33 (catálogo de servicios), US-34 (exportación y envío de reportes) | `Steps/AdminDashboardSteps.cs`, `Steps/StaffPerformanceSteps.cs`, `Steps/ServiceCatalogSteps.cs` |

`Steps/OperationsSteps.cs` usa `Support/OperationsWorld.cs`: el servicio Accommodations **cableado como en producción**
(los mismos `Add…ContextServices` de `Program.cs`: casos de uso, repositorios, manejadores de eventos y notificaciones
push/correo) sobre una base de datos en memoria. Solo se reemplazan los otros servicios (IAM, Bookings) y los canales de
salida (push, correo, RabbitMQ) por dobles que registran lo enviado. Cada paso es una "petición" con su propio scope de
DI y su propio `DbContext`.

```sh
dotnet test tests/RoomTrack.Accommodations.API.Tests --filter "Category=Sprint3"
dotnet test tests/RoomTrack.Accommodations.API.Tests --filter "Category=US30"
```

## Trazabilidad del Sprint 3 (y US-07)

| US / escenario | Estado | Evidencia |
| --- | --- | --- |
| US-07 / E01 | **Ejecutable** | La tarea copia la checklist del tipo de habitación (`PUT /cleaning-tasks/checklists?roomTypeId=`). |
| US-07 / E02 | **Ejecutable** | `PUT /cleaning-tasks/{id}/checklist/{itemId}` y `checklistProgress` (0-100). |
| US-07 / E03 | **Ejecutable** | Completar con pasos obligatorios pendientes → 409 `cleaning_task.checklist_incomplete` con `pendingItems`. |
| US-10 / E01 | **Ejecutable** | `GET /rooms/{id}/maintenance-history`: incidencias con fechas y estados. |
| US-10 / E02 | **Ejecutable** | Más de 3 incidencias iguales en 30 días → `isRecurring` y push urgente a los administradores. |
| US-30 / E01 | **Ejecutable** | `POST /preventive-maintenance-plans` + job `POST /preventive-maintenance-plans/runs` genera la tarea (incidencia `Preventive`). |
| US-30 / E02 | **Ejecutable** | El job envía un recordatorio por ciclo, `reminderDaysBefore` días antes. |
| US-30 / E03 | **Ejecutable** | El historial de la habitación muestra correctivas y preventivas. |
| US-11 / E01–E04 | **Ejecutable** | Registro, derivación (push al área), tablero y confirmación al huésped. |
| US-19 / E01–E03 | **Ejecutable** | Solicitud desde la app, confirmación con tiempo estimado y seguimiento de estado. |
| US-20 / E01–E02 | **Ejecutable** | Chat huésped ↔ recepción en la misma conversación y push de la respuesta. |
| US-12 / E01–E03 | **Ejecutable** | Comentarios por tarea (push al responsable), canal del área e historial de comentarios y estados. |
| US-32 / E01–E03 | **Ejecutable** | `POST /shift-handovers`, `GET /shift-handovers/latest`, `POST /shift-handovers/{id}/acknowledge`. |
| US-23 / E01 | **Ejecutable** | Job `POST /rooms/arrival-readiness-alerts`: si la habitación de una llegada de hoy no está lista 2 h antes del check-in, administración y recepción reciben push y correo (una alerta por llegada). |
| US-23 / E02 | **Ejecutable** | Incidencia crítica no atendida a tiempo → escalamiento automático (job `POST /incidents/escalations`). |
| US-23 / E03 | **Ejecutable** | `GET /incidents/alerts`: alertas (escalamientos y recurrencias) con su resolución. |

US-31 (turnos) vive en el servicio Profiles: `tests/RoomTrack.Profiles.API.Tests/Features/Sprint3/US-31.feature`.

## Trazabilidad del Sprint 4 (US-18, US-24, US-25, US-33, US-34)

Ambos reportes se calculan con las limpiezas completadas, las incidencias resueltas y las solicitudes de huésped
resueltas que ya registra Accommodations (contexto `StaffPerformance`, sin tablas propias). Requieren `Admin` o
`ChainAdmin` (`ViewAnalytics`); sin fechas cubren los últimos 30 días (máximo 366).

| US / escenario | Estado | Evidencia |
| --- | --- | --- |
| US-18 / E01 | **Ejecutable** | `GET /hotels/{id}/dashboard`: habitaciones por estado y ocupación, limpieza, incidencias y lo que requiere atención en una sola respuesta. |
| US-18 / E02 | **Ejecutable** | Tareas de limpieza atrasadas (abiertas más de `overdueAfterMinutes`, 60 por defecto). |
| US-18 / E03 | **Ejecutable** | `frontDesk`: llegadas, alojados y salidas de hoy y las últimas evaluaciones (API interna de Bookings). |
| US-18 / E04 | **Ejecutable** | Si Bookings no responde, el panel igual responde con `frontDesk: null`. |
| US-24 / E01 | **Ejecutable** | `GET /staff-performance/productivity`: tareas completadas por colaborador, el más productivo primero. |
| US-24 / E02 | **Ejecutable** | `areas`: recepción, limpieza y mantenimiento siempre presentes, con colaboradores y promedio por colaborador. |
| US-24 / E03 | **Ejecutable** | Solo cuenta lo terminado dentro de `from`–`to`. |
| US-25 / E01 | **Ejecutable** | `GET /staff-performance/service-times`: espera, trabajo y tiempo total promedio de limpieza. |
| US-25 / E02 | **Ejecutable** | Lo mismo para incidencias correctivas (las preventivas se generan por adelantado y no se miden). |
| US-25 / E03 | **Ejecutable** | Sin actividad: cantidad 0 y promedios `null`. |
| US-33 / E01 | **Ejecutable** | `GET /service-catalog`: el huésped ve los servicios disponibles de su hotel con precio, área y tiempo estimado. |
| US-33 / E02 | **Ejecutable** | `POST /guest-requests` con `serviceItemId`: la solicitud va al área del servicio con su nombre, precio y tiempo. |
| US-33 / E03 | **Ejecutable** | Un servicio retirado (`isAvailable: false`) → 409 `service_catalog.not_available`. |
| US-33 / E04 | **Ejecutable** | La solicitud guarda el precio del momento; cambiar el catálogo (`PUT /service-catalog/{id}`) no la altera. |
| US-34 / E01 | **Ejecutable** | `GET /staff-performance/productivity/export` (también `service-times/export`; en Bookings, `stay-reviews/export` y `stay-reviews/satisfaction/export`). |
| US-34 / E02 | **Ejecutable** | `POST /staff-performance/report-schedules` + job `POST /staff-performance/report-schedules/runs`: correo semanal o mensual con el resumen. |
| US-34 / E03 | **Ejecutable** | Antes de `nextRunAt` el job no envía nada. |
