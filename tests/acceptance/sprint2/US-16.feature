# language: es
@Sprint2 @US16
Característica: US-16 — Pago de cargos adicionales y check-out
  Criterios de aceptación del informe Avance 3, corte 01/11/2026.
  Diseño BDD pendiente de bindings y ejecución integral; no acredita cierre de la US.

  @US16_E01 @pendiente_aceptacion @sin_binding
  Escenario: Revisión del folio
    Dado que voy a hacer check-out
    Cuando reviso mi folio
    Entonces veo el detalle de todos los cargos antes de pagar

  @US16_E02 @pendiente_aceptacion @sin_binding
  Escenario: Pago del saldo pendiente
    Dado que tengo un saldo pendiente
    Cuando confirmo el pago
    Entonces el sistema lo procesa y actualiza mi folio a "saldado"

  @US16_E03 @pendiente_aceptacion @sin_binding
  Escenario: División de pago
    Dado que quiero pagar con dos métodos distintos
    Cuando distribuyo el monto entre ambos
    Entonces el sistema procesa ambos pagos correctamente

  @US16_E04 @pendiente_aceptacion @sin_binding
  Escenario: Liberación del depósito
    Dado que no tengo cargos adicionales al depósito de garantía
    Cuando hago check-out
    Entonces el sistema inicia automáticamente la devolución del depósito
