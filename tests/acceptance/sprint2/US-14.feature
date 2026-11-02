# language: es
@Sprint2 @US14
Característica: US-14 — Check-out digital del huésped
  Criterios de aceptación del informe Avance 3, corte 01/11/2026.
  Diseño BDD pendiente de bindings y ejecución integral; no acredita cierre de la US.

  @US14_E01 @pendiente_aceptacion @sin_binding
  Escenario: Check-out exitoso
    Dado que finalizo mi estancia sin cargos pendientes
    Cuando confirmó el check-out desde la app
    Entonces mi habitación se libera y pasa a estado "pendiente de limpieza"

  @US14_E02 @pendiente_aceptacion @sin_binding
  Escenario: Check-out con cargos pendientes
    Dado que tengo consumos adicionales sin pagar
    Cuando intento hacer check-out
    Entonces el sistema me muestra el detalle de los cargos antes de permitirme finalizar

  @US14_E03 @pendiente_aceptacion @sin_binding
  Escenario: Confirmación y comprobante
    Dado que completo el check-out
    Cuando se procesa
    Entonces recibo un comprobante digital de mi estancia por correo

  @US14_E04 @pendiente_aceptacion @sin_binding
  Escenario: Extensión de estancia
    Dado que quiero quedarme más tiempo
    Cuando lo solicite antes del check-out
    Entonces el sistema verifica disponibilidad y actualiza mi reserva
