# Sprint 2 — diseño de pruebas de aceptación

Avance 3, 01/11/2026 (America/Lima). Alcance: US-27, US-13, US-15, US-16, US-17 y US-14; 42 puntos / 34 horas.

Se incluyen seis archivos Gherkin en español con los 23 escenarios de aceptación del informe. Son especificaciones de pruebas: todavía no tienen bindings ni runner BDD conectado a los Web Services. `@sin_binding` y `@pendiente_aceptacion` hacen explícita esa limitación; no son pruebas aprobadas ni pruebas omitidas de la suite .NET. No se alteran ni reducen los criterios para ajustarlos a lo implementado.

## Evidencia ejecutada que se conserva

- Backend main `db77552b664a47634ccbc89dc54fb5617694c466`: CI [40792588549](https://github.com/RoomTrack/RoomTrack-BackEnd/actions/runs/40792588549), 268 pruebas aprobadas, cero fallidas y cero omitidas. Bookings aporta 25; la cifra global no corresponde exclusivamente al Sprint 2. No se volvió a ejecutar esta suite al redactar los .feature.
- La suite existente usa xUnit/FluentAssertions. `BookingCheckOutTests` comprueba salida del huésped, salida asistida, rechazo de otro hotel/huésped y rechazo sin check-in o por segundo check-out; `BookingsPersistenceTests` comprueba persistencia y paginación con SQLite; `BookingTests` comprueba reservas y transiciones. No validan una pasarela, depósito o emisión de factura.
- Postman Desktop, 01/11/2026: 16 solicitudes al backend local aislado `http://localhost:18080/api/v1`, 23 validaciones técnicas aprobadas y cero fallidas. Las 23 validaciones no son los 23 escenarios de aceptación. Se probaron registro manual del pago, confirmación de reserva, asistencia, check-in digital/asistido y check-out digital/asistido; el estado de habitación se consultó después de la salida.
- [Colección Postman](https://go.postman.co/collection/35638271-c0fa25e3-7af6-48c3-a452-675a0271551c). Las capturas y resultados están incorporados en §5.3.2.4 del [informe](https://docs.google.com/document/d/1Z2EGbbdus1PveWczTfVqXrJz9heHpMjnH3Mt2MJE1fI/edit).

## Cómo ejecutar la suite existente

Con SDK .NET 9 y las dependencias del repositorio:

```sh
dotnet test tests/RoomTrack.Bookings.API.Tests/RoomTrack.Bookings.API.Tests.csproj --configuration Release --logger trx
```

Esto ejecuta xUnit, no los archivos .feature. Para ejecutar BDD falta implementar bindings y un runner, preparar datos ficticios aislados y registrar resultados por escenario. No usar producción para los escenarios de pagos y check-out.

## Trazabilidad

| US / escenario | Estado de aceptación | Evidencia técnica y límite |
| --- | --- | --- |
| US-13 / E01 | Pendiente de aceptación integral | Divergencia de comportamiento: La API pasa directamente a CheckedIn después de completar el documento; no persiste el estado previo «check-in en proceso» descrito. El modelo demo sí tiene esa etapa. |
| US-13 / E02 | Pendiente de aceptación integral | Parcial: El documento y el código de acceso están conectados y probados en API; no existe una segunda confirmación de llegada equivalente al criterio completo. |
| US-13 / E03 | Pendiente de aceptación integral | Solo demo / brecha integrada: La estimación ETA está en HotelStore; el backend devuelve rechazo por habitación no lista con mensaje genérico. No se acreditó una ETA calculada en el flujo conectado. |
| US-13 / E04 | Pendiente de aceptación integral | Conectado; API verificada: La pantalla remota llama a requestCheckInAssistance; Postman obtuvo 202. No se realizó una nueva prueba visual de esa pantalla en esta revisión. |
| US-14 / E01 | Pendiente de aceptación integral | Parcial: API comprobada, UI pendiente: Postman acredita Completed y habitación Cleaning. En modo conectado el huésped recibe la indicación de contactar a recepción, sin botón que invoque el endpoint check-out. |
| US-14 / E02 | Pendiente de aceptación integral | Solo demo / brecha integrada: Folio, saldo y bloqueo local existen en HotelStore; no hay folio de cargos conectado a la salida de la API revisada. |
| US-14 / E03 | Pendiente de aceptación integral | Parcial / entrega no acreditada: El backend implementa correo-resumen de salida. La función demo registra un mensaje en outbox. No se acreditó recepción de comprobante por correo ni el flujo móvil completo. |
| US-14 / E04 | Pendiente de aceptación integral | Solo demo / flujo activo no conectado: La extensión de la app cambia la estancia y añade cargos localmente. El modo conectado remite a recepción; no implementa la extensión solicitada por el huésped desde esa pantalla. |
| US-15 / E01 | Pendiente de aceptación integral | No integrada: El adaptador registrado es ManualPaymentGateway y aprueba el registro del pago informado por el personal; no procesa la tarjeta del huésped en una pasarela externa. |
| US-15 / E02 | Pendiente de aceptación integral | No acreditada como rechazo de pasarela: El rechazo por sufijo de tarjeta es simulación en HotelStore. El HTTP 409 de pago duplicado en Postman es una regla de reserva ya pagada, no un rechazo del emisor ni un reintento con otro medio. |
| US-15 / E03 | Pendiente de aceptación integral | Solo demo / brecha integrada: HotelStore simula retención; la sincronización remota fija depositRequired en 0 y señala que el backend carece de depósito de garantía. |
| US-15 / E04 | Pendiente de aceptación integral | Parcial integrado y probado: Registrar el pago confirma la reserva y habilita el check-in; está respaldado por Postman y por la prueba live previa. Existe correo de confirmación, sin entrega externa acreditada. |
| US-16 / E01 | Pendiente de aceptación integral | Solo demo / parcial local: Se arma el cargo de habitación desde totalPrice, pero los cargos agregados en el dispositivo se conservan localmente hasta que exista folio backend. |
| US-16 / E02 | Pendiente de aceptación integral | Solo demo / brecha integrada: payBalance actualiza arrays locales; el endpoint Payments registra el total de la reserva, sin liquidar el saldo de un folio de cargos. |
| US-16 / E03 | Pendiente de aceptación integral | Solo demo / brecha integrada: La división de pago recorre partes localmente. El pago backend actual no admite dividir un saldo: registra totalPrice y rechaza otro pago completado de la misma reserva. |
| US-16 / E04 | Pendiente de aceptación integral | Solo demo / brecha integrada: La devolución del depósito se añade localmente como Payment refunded; falta retención y devolución integrada de esa garantía. |
| US-17 / E01 | Pendiente de aceptación integral | Solo demo / emisión no integrada: _issueReceipt agrega un Receipt local. El registro de pago consultado por API no equivale a emitir el comprobante solicitado. |
| US-17 / E02 | Pendiente de aceptación integral | Solo demo / brecha integrada: Los datos RUC/razón social y numeración F001 se mantienen en HotelStore; no se identificó emisión persistida correspondiente en la API revisada. |
| US-17 / E03 | Pendiente de aceptación integral | Solo demo / brecha integrada: resendReceipt incrementa sentCount y usa _mail, que solo agrega un OutboxEmail al estado local. |
| US-17 / E04 | Pendiente de aceptación integral | Parcial: La API permite consultar un pago de una reserva y la app muestra pagos cargados. No acredita el historial completo de cobros, reembolsos y comprobantes emitidos requerido por el criterio. |
| US-27 / E01 | Pendiente de aceptación integral | Parcial: API comprobada, UI pendiente: El endpoint admite recepción y quedó probado en Postman. La pantalla de recepción en modo conectado no ofrece ese check-in; deriva al check-in digital del huésped. |
| US-27 / E02 | Pendiente de aceptación integral | Solo demo para cobro de cargos y folio: La API permite salida asistida y quedó probada, pero el pago realizado fue de la reserva, no de cargos pendientes de un folio. En recepción conectada no hay esa liquidación ni check-out integrado. |
| US-27 / E03 | Pendiente de aceptación integral | Implementada parcialmente; sin cierre: El backend tiene FindAlternativeRoomsAsync. El flujo de alternativas de la pantalla asistida pertenece a la rama demo; no hay evidencia API de este escenario en las 16 capturas nuevas. |

## Límites de aceptación

Las seis historias continúan En curso. El pago manual no es pasarela; un HTTP 409 por segundo pago no es rechazo bancario. La consulta de pago no es comprobante. Siguen pendientes pre check-in, espera estimada integrada, operaciones móviles conectadas, folio/cargos, división de pagos, garantía, comprobantes y reenvío real conforme a cada escenario. Esta incorporación documenta pruebas diseñadas y evidencia parcial; no cierra las historias.
