# language: es
@Sprint2 @US15
Característica: US-15 — Pago de reserva durante el check-in
  Criterios de aceptación del informe (TF1, corte 25/11/2026).
  La pasarela de pagos integrada corre en modo sandbox (SandboxCardPaymentGateway): sus tokens de prueba aprueban o
  rechazan la tarjeta como lo haría la pasarela real.

  @US15_E01
  Escenario: Pago con tarjeta
    Dado que estoy completando mi check-in
    Cuando ingreso los datos de mi tarjeta
    Entonces el sistema procesa el pago de forma segura mediante la pasarela de pagos integrada

  @US15_E02
  Escenario: Pago rechazado
    Dado que mi método de pago es rechazado
    Cuando esto ocurre
    Entonces el sistema me notifica el motivo y me permite intentar otro medio de pago

  @US15_E03
  Escenario: Depósito de garantía
    Dado que el hotel requiere un depósito reembolsable
    Cuando completo el pago
    Entonces el monto queda retenido y visible en mi folio de huésped

  @US15_E04
  Escenario: Confirmación de pago
    Dado que el pago se procesa correctamente
    Cuando finaliza
    Entonces recibo una confirmación y mi check-in queda habilitado para completarse
