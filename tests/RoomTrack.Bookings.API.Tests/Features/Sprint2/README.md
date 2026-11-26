# Sprint 2 — pruebas de aceptación (BDD)

Alcance: US-27, US-13, US-15, US-16, US-17 y US-14 (23 escenarios del informe; trazabilidad actualizada al corte del
TF1, 25/11/2026).

Los seis `.feature` se ejecutan con **Reqnroll** como tests de xUnit en `dotnet test` (y por tanto en la CI). Hay dos
clases de step definitions:

- `../Steps/StaySteps.cs`: check-in y check-out por el caso de uso real (`CheckInService`) y el dominio de Bookings.
- `../Steps/FolioSteps.cs`: folio, depósito, pagos divididos, comprobantes e historial por los casos de uso reales del
  contexto Payments (`FolioCommandService`, `FolioQueryService`), con el **mismo cableado de inyección de dependencias**
  y los **manejadores de eventos de producción** (comprobante automático en cada pago, cierre del folio al hacer
  check-out) y el `UnitOfWork` real que publica los eventos dentro de la transacción.

Ambas corren sobre una base de datos en memoria. Las habitaciones viven en el servicio Accommodations: el doble de
prueba sigue su contrato ACL (un check-in requiere la habitación `Available` y la deja `Occupied`; un check-out la pasa
de `Occupied` a `Cleaning`).

- **Escenarios ejecutables** (sin `@ignore`): pasan o fallan según el comportamiento real del backend.
- **`@pendiente_implementacion @ignore`**: la funcionalidad no existe todavía en la API. Reqnroll los reporta como
  *omitidos*; no son pruebas aprobadas. Cada uno indica en un comentario qué falta.

```sh
dotnet test tests/RoomTrack.Bookings.API.Tests --filter "Category=Sprint2"
```

## Trazabilidad

| US / escenario | Estado | Evidencia o brecha |
| --- | --- | --- |
| US-13 / E01 | Omitido | La API pasa directamente a CheckedIn al aprobar el documento; no persiste el estado «check-in en proceso». |
| US-13 / E02 | Omitido | No existe una segunda confirmación de identidad al llegar. |
| US-13 / E03 | Omitido | La API rechaza el check-in con la habitación no lista, pero no calcula el tiempo estimado de espera. |
| US-13 / E04 | **Ejecutable** | `RequestCheckInAssistanceCommand` registra la solicitud para recepción del hotel (evento que notifica al personal). |
| US-14 / E01 | **Ejecutable** | Check-out del huésped: la reserva queda Completed y la habitación pasa a Cleaning («pendiente de limpieza»). |
| US-14 / E02 | **Ejecutable** | Con consumos sin pagar el check-out se rechaza con `folio.balance_pending`, el saldo y el detalle de los cargos. |
| US-14 / E03 | **Ejecutable** | Al hacer check-out el folio se cierra y el huésped recibe el estado de cuenta con sus comprobantes. |
| US-14 / E04 | Omitido | El huésped no puede extender la estancia desde la app conectada. |
| US-15 / E01 | Omitido | El adaptador registrado es `ManualPaymentGateway`: no hay pasarela de tarjetas. |
| US-15 / E02 | Omitido | Sin pasarela no hay rechazo del emisor. |
| US-15 / E03 | **Ejecutable** | `POST /bookings/{id}/folio/deposit` retiene el depósito y queda visible en el folio (no suma al saldo). |
| US-15 / E04 | **Ejecutable** | El pago completado confirma la reserva (evento de confirmación → correo) y habilita el check-in. |
| US-16 / E01 | **Ejecutable** | `GET /bookings/{id}/folio` muestra cada cargo, el total y el saldo antes de pagar. |
| US-16 / E02 | **Ejecutable** | Pagar el saldo deja el folio `Settled` («saldado») y emite su comprobante. |
| US-16 / E03 | **Ejecutable** | `POST /bookings/{id}/folio/payments` acepta varias partes (p. ej. Yape + efectivo): un pago y un comprobante por parte. |
| US-16 / E04 | **Ejecutable** | Sin cargos pendientes, el check-out cierra el folio y libera el depósito (inicia la devolución). |
| US-17 / E01 | **Ejecutable** | Cada pago completado genera automáticamente su boleta (`B001-…`) con IGV y la envía por correo. |
| US-17 / E02 | **Ejecutable** | Con datos fiscales (`PUT /bookings/{id}/folio/invoice-data`) el comprobante es una factura `F001-…` con RUC y razón social. |
| US-17 / E03 | **Ejecutable** | `POST /receipts/{id}/resend` reenvía el comprobante al correo del huésped (`sentCount` aumenta). |
| US-17 / E04 | **Ejecutable** | `GET /payments/history` lista cobros, reembolsos, depósitos y comprobantes por fecha. |
| US-27 / E01 | **Ejecutable** | Recepción completa el check-in del huésped: la reserva queda CheckedIn y la habitación Occupied («ocupada»). |
| US-27 / E02 | **Ejecutable** | Recepción cobra el saldo y hace el check-out en el mismo flujo: folio saldado y habitación «pendiente de limpieza». |
| US-27 / E03 | **Ejecutable** | Con la habitación aún en limpieza el check-in se rechaza sin dejar cambios y se sugieren habitaciones listas. |

Resultado actual: **17 escenarios ejecutables aprobados y 6 omitidos** (US-13 E01–E03, US-14 E04, US-15 E01–E02).
US-16 y US-17 quedan completas; US-13, US-14 y US-15 siguen parciales.
