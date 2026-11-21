# language: es
@Sprint2 @US16
Característica: US-16 — Pago de cargos adicionales y check-out
  Criterios de aceptación del informe (Avance 3, corte 01/11/2026).
  Los escenarios con @ignore aún no tienen implementación en la API (ver README.md): Reqnroll los reporta como
  omitidos, no como aprobados.

  @US16_E01 @pendiente_implementacion @ignore
  Escenario: Revisión del folio
    # Requiere el folio de cargos adicionales, que la API aún no tiene.
    Dado que voy a hacer check-out
    Cuando reviso mi folio
    Entonces veo el detalle de todos los cargos antes de pagar

  @US16_E02 @pendiente_implementacion @ignore
  Escenario: Pago del saldo pendiente
    # Requiere el folio de cargos adicionales, que la API aún no tiene.
    Dado que tengo un saldo pendiente
    Cuando confirmo el pago
    Entonces el sistema lo procesa y actualiza mi folio a "saldado"

  @US16_E03 @pendiente_implementacion @ignore
  Escenario: División de pago
    # Requiere el folio de cargos adicionales, que la API aún no tiene.
    Dado que quiero pagar con dos métodos distintos
    Cuando distribuyo el monto entre ambos
    Entonces el sistema procesa ambos pagos correctamente

  @US16_E04 @pendiente_implementacion @ignore
  Escenario: Liberación del depósito
    # Requiere el folio de cargos adicionales, que la API aún no tiene.
    Dado que no tengo cargos adicionales al depósito de garantía
    Cuando hago check-out
    Entonces el sistema inicia automáticamente la devolución del depósito
