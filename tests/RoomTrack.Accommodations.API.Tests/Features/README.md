# Accommodations — pruebas de aceptación (BDD)

Los `.feature` (Gherkin en español) se ejecutan con **Reqnroll** como tests de xUnit en `dotnet test` (y en la CI).

| Carpeta | Historias | Step definitions |
| --- | --- | --- |
| `Sprint1/` | US-05, US-06 (limpieza), US-07 (checklist), US-08, US-09 (incidencias) | `Steps/HousekeepingSteps.cs`, `Steps/IncidentSteps.cs`, `Steps/OperationsSteps.cs` |
| `Sprint3/` | US-10, US-30, US-11, US-19, US-20, US-12, US-32, US-23 | `Steps/OperationsSteps.cs` |

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
| US-23 / E01 | Omitido | La alerta por checkout próximo sin limpiar requiere cruzar llegadas/salidas (Bookings) con la limpieza. |
| US-23 / E02 | **Ejecutable** | Incidencia crítica no atendida a tiempo → escalamiento automático (job `POST /incidents/escalations`). |
| US-23 / E03 | **Ejecutable** | `GET /incidents/alerts`: alertas (escalamientos y recurrencias) con su resolución. |

US-31 (turnos) vive en el servicio Profiles: `tests/RoomTrack.Profiles.API.Tests/Features/Sprint3/US-31.feature`.
