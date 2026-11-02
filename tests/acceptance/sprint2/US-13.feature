# language: es
@Sprint2 @US13
Característica: US-13 — Check-in digital del huésped
  Criterios de aceptación del informe Avance 3, corte 01/11/2026.
  Diseño BDD pendiente de bindings y ejecución integral; no acredita cierre de la US.

  @US13_E01 @pendiente_aceptacion @sin_binding
  Escenario: Check-in previo a la llegada
    Dado que tengo una reserva confirmada
    Cuando completo el check-in digital antes de llegar
    Entonces mi reserva queda marcada como "check-in en proceso"

  @US13_E02 @pendiente_aceptacion @sin_binding
  Escenario: Confirmación al llegar
    Dado que hice el check-in digital
    Cuando llego al hotel
    Entonces solo debo confirmar mi identidad para recibir la llave o código de acceso a mi habitación

  @US13_E03 @pendiente_aceptacion @sin_binding
  Escenario: Habitación aún no disponible
    Dado que mi habitación no está lista al momento de mi llegada
    Cuando intentó completar el check-in
    Entonces el sistema me informa el tiempo estimado de espera

  @US13_E04 @pendiente_aceptacion @sin_binding
  Escenario: Asistencia de recepción
    Dado que tengo un problema durante el check-in digital
    Cuando solicitó ayuda
    Entonces puedo contactar directamente a recepción desde la misma pantalla
