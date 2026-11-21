# Sprint 2 — pruebas de aceptación (BDD)

Alcance: US-27, US-13, US-15, US-16, US-17 y US-14 (23 escenarios del informe, corte 01/11/2026).

Los seis `.feature` se ejecutan con **Reqnroll** como tests de xUnit en `dotnet test` (y por tanto en la CI). Las
step definitions están en `../Steps/StaySteps.cs` y recorren el caso de uso real de check-in/check-out
(`CheckInService`) y el dominio de Bookings sobre una base de datos en memoria. Las habitaciones viven en el servicio
Accommodations: el doble de prueba sigue su contrato ACL (un check-in requiere la habitación `Available` y la deja
`Occupied`; un check-out la pasa de `Occupied` a `Cleaning`).

- **Escenarios ejecutables** (sin `@ignore`): pasan o fallan según el comportamiento real del backend.
- **`@pendiente_implementacion @ignore`**: la funcionalidad no existe todavía en la API. Reqnroll los reporta como
  *omitidos*; no son pruebas aprobadas. Cada uno indica en un comentario qué falta.

No se alteraron los criterios para ajustarlos a lo implementado; solo se corrigieron erratas de redacción
("intentó/solicitó/confirmó/registró" → "intento/solicito/confirmo/registro").

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
| US-14 / E02 | Omitido | Requiere folio de cargos adicionales (US-16). |
| US-14 / E03 | Omitido | Requiere emisión de comprobantes (US-17). |
| US-14 / E04 | Omitido | El huésped no puede extender la estancia desde la app conectada. |
| US-15 / E01 | Omitido | El adaptador registrado es `ManualPaymentGateway`: no hay pasarela de tarjetas. |
| US-15 / E02 | Omitido | Sin pasarela no hay rechazo del emisor. |
| US-15 / E03 | Omitido | No hay depósito de garantía. |
| US-15 / E04 | **Ejecutable** | El pago completado confirma la reserva (evento de confirmación → correo) y habilita el check-in. |
| US-16 / E01–E04 | Omitido | Requiere folio, liquidación de saldo, división de pago y devolución de depósito. |
| US-17 / E01–E04 | Omitido | Requiere emisión, datos fiscales, reenvío e historial de comprobantes. |
| US-27 / E01 | **Ejecutable** | Recepción completa el check-in del huésped: la reserva queda CheckedIn y la habitación Occupied («ocupada»). |
| US-27 / E02 | Omitido | Requiere folio de cargos adicionales (US-16). |
| US-27 / E03 | **Ejecutable** | Con la habitación aún en limpieza el check-in se rechaza sin dejar cambios y se sugieren habitaciones listas. |

Resultado actual: 5 escenarios ejecutables aprobados y 18 omitidos. Las historias siguen En curso hasta implementar
lo pendiente.
