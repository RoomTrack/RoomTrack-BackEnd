# language: es
@Sprint2 @US15
Característica: US-15 — Pago de reserva durante el check-in
  Criterios de aceptación del informe Avance 3, corte 01/11/2026.
  Diseño BDD pendiente de bindings y ejecución integral; no acredita cierre de la US.

  @US15_E01 @pendiente_aceptacion @sin_binding
  Escenario: Pago con tarjeta
    Dado que estoy completando mi check-in
    Cuando ingreso los datos de mi tarjeta
    Entonces el sistema procesa el pago de forma segura mediante la pasarela de pagos integrada

  @US15_E02 @pendiente_aceptacion @sin_binding
  Escenario: Pago rechazado
    Dado que mi método de pago es rechazado
    Cuando esto ocurre
    Entonces el sistema me notifica el motivo y me permite intentar otro medio de pago

  @US15_E03 @pendiente_aceptacion @sin_binding
  Escenario: Depósito de garantía
    Dado que el hotel requiere un depósito reembolsable
    Cuando completo el pago
    Entonces el monto queda retenido y visible en mi folio de huésped

  @US15_E04 @pendiente_aceptacion @sin_binding
  Escenario: Confirmación de pago
    Dado que el pago se procesa correctamente
    Cuando finaliza
    Entonces recibo una confirmación y mi check-in queda habilitado para completarse
