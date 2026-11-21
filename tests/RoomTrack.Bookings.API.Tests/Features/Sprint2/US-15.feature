# language: es
@Sprint2 @US15
Característica: US-15 — Pago de reserva durante el check-in
  Criterios de aceptación del informe (Avance 3, corte 01/11/2026).
  Los escenarios con @ignore aún no tienen implementación en la API (ver README.md): Reqnroll los reporta como
  omitidos, no como aprobados.

  @US15_E01 @pendiente_implementacion @ignore
  Escenario: Pago con tarjeta
    # El adaptador registrado es ManualPaymentGateway: no hay pasarela de tarjetas integrada.
    Dado que estoy completando mi check-in
    Cuando ingreso los datos de mi tarjeta
    Entonces el sistema procesa el pago de forma segura mediante la pasarela de pagos integrada

  @US15_E02 @pendiente_implementacion @ignore
  Escenario: Pago rechazado
    # Sin pasarela no hay rechazo del emisor.
    Dado que mi método de pago es rechazado
    Cuando esto ocurre
    Entonces el sistema me notifica el motivo y me permite intentar otro medio de pago

  @US15_E03 @pendiente_implementacion @ignore
  Escenario: Depósito de garantía
    # La API no maneja depósito de garantía.
    Dado que el hotel requiere un depósito reembolsable
    Cuando completo el pago
    Entonces el monto queda retenido y visible en mi folio de huésped

  @US15_E04
  Escenario: Confirmación de pago
    Dado que el pago se procesa correctamente
    Cuando finaliza
    Entonces recibo una confirmación y mi check-in queda habilitado para completarse
