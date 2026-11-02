# language: es
@Sprint2 @US27
Característica: US-27 — Check-in y check-out asistidos por recepción
  Criterios de aceptación del informe Avance 3, corte 01/11/2026.
  Diseño BDD pendiente de bindings y ejecución integral; no acredita cierre de la US.

  @US27_E01 @pendiente_aceptacion @sin_binding
  Escenario: Check-in presencial
    Dado que un huésped llega sin haber hecho check-in digital
    Cuando registró sus datos y confirmó la reserva
    Entonces la habitación pasa a estado "ocupada"

  @US27_E02 @pendiente_aceptacion @sin_binding
  Escenario: Check-out presencial con cobro
    Dado que un huésped hace check-out en recepción con cargos pendientes
    Cuando proceso su pago desde el mismo módulo
    Entonces su folio queda saldado y la habitación pasa a "pendiente de limpieza"

  @US27_E03 @pendiente_aceptacion @sin_binding
  Escenario: Check-in anticipado no disponible
    Dado que un huésped llega antes de que su habitación esté lista
    Cuando intento hacer check-in
    Entonces el sistema me avisa y sugiere habitaciones alternativas disponibles
