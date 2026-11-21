# language: es
@Sprint2 @US27
Característica: US-27 — Check-in y check-out asistidos por recepción
  Criterios de aceptación del informe (Avance 3, corte 01/11/2026).
  Los escenarios con @ignore aún no tienen implementación en la API (ver README.md): Reqnroll los reporta como
  omitidos, no como aprobados.

  @US27_E01
  Escenario: Check-in presencial
    Dado que un huésped llega sin haber hecho check-in digital
    Cuando registro sus datos y confirmo la reserva
    Entonces la habitación pasa a estado "ocupada"

  @US27_E02 @pendiente_implementacion @ignore
  Escenario: Check-out presencial con cobro
    # Requiere el folio de cargos adicionales (US-16).
    Dado que un huésped hace check-out en recepción con cargos pendientes
    Cuando proceso su pago desde el mismo módulo
    Entonces su folio queda saldado y la habitación pasa a "pendiente de limpieza"

  @US27_E03
  Escenario: Check-in anticipado no disponible
    Dado que un huésped llega antes de que su habitación esté lista
    Cuando intento hacer check-in
    Entonces el sistema me avisa y sugiere habitaciones alternativas disponibles
