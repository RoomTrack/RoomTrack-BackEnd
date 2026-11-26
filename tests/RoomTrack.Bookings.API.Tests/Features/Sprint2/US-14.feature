# language: es
@Sprint2 @US14
Característica: US-14 — Check-out digital del huésped
  Criterios de aceptación del informe (TF1, corte 25/11/2026).
  Los escenarios con @ignore aún no tienen implementación en la API (ver README.md): Reqnroll los reporta como
  omitidos, no como aprobados.

  @US14_E01
  Escenario: Check-out exitoso
    Dado que finalizo mi estancia sin cargos pendientes
    Cuando confirmo el check-out desde la app
    Entonces mi habitación se libera y pasa a estado "pendiente de limpieza"

  @US14_E02
  Escenario: Check-out con cargos pendientes
    Dado que tengo consumos adicionales sin pagar
    Cuando intento hacer check-out
    Entonces el sistema me muestra el detalle de los cargos antes de permitirme finalizar

  @US14_E03
  Escenario: Confirmación y comprobante
    Dado que completo el check-out
    Cuando se procesa
    Entonces recibo un comprobante digital de mi estancia por correo

  @US14_E04 @pendiente_implementacion @ignore
  Escenario: Extensión de estancia
    # El huésped no puede extender su estancia desde la app conectada.
    Dado que quiero quedarme más tiempo
    Cuando lo solicito antes del check-out
    Entonces el sistema verifica disponibilidad y actualiza mi reserva
