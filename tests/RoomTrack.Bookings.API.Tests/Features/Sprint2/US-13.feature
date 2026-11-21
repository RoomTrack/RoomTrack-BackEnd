# language: es
@Sprint2 @US13
Característica: US-13 — Check-in digital del huésped
  Criterios de aceptación del informe (Avance 3, corte 01/11/2026).
  Los escenarios con @ignore aún no tienen implementación en la API (ver README.md): Reqnroll los reporta como
  omitidos, no como aprobados.

  @US13_E01 @pendiente_implementacion @ignore
  Escenario: Check-in previo a la llegada
    # La API pasa directamente a CheckedIn al aprobar el documento: no existe el estado "check-in en proceso".
    Dado que tengo una reserva confirmada
    Cuando completo el check-in digital antes de llegar
    Entonces mi reserva queda marcada como "check-in en proceso"

  @US13_E02 @pendiente_implementacion @ignore
  Escenario: Confirmación al llegar
    # No existe una segunda confirmación de identidad al llegar.
    Dado que hice el check-in digital
    Cuando llego al hotel
    Entonces solo debo confirmar mi identidad para recibir la llave o código de acceso a mi habitación

  @US13_E03 @pendiente_implementacion @ignore
  Escenario: Habitación aún no disponible
    # La API rechaza el check-in, pero no calcula el tiempo estimado de espera.
    Dado que mi habitación no está lista al momento de mi llegada
    Cuando intento completar el check-in
    Entonces el sistema me informa el tiempo estimado de espera

  @US13_E04
  Escenario: Asistencia de recepción
    Dado que tengo un problema durante el check-in digital
    Cuando solicito ayuda
    Entonces puedo contactar directamente a recepción desde la misma pantalla
