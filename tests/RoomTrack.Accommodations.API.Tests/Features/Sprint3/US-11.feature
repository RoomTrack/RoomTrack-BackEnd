# language: es
@Sprint3 @US11
Característica: US-11 — Registro y derivación de solicitudes de huéspedes
  Como recepcionista
  quiero registrar y derivar las solicitudes de los huéspedes al área correspondiente
  para que sean atendidas sin demoras.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 305
    Y "Rocío" trabaja en recepción
    Y "Ana" trabaja en limpieza
    Y "Mario" trabaja en mantenimiento
    Y el huésped "Luis" se aloja en la habitación 305

  @US11_E01
  Escenario: Registro de solicitud
    Dado que un huésped pide "Almohada extra"
    Cuando registro la solicitud para el área de "limpieza"
    Entonces se crea una tarea con el área destino asignada

  @US11_E02
  Escenario: Derivación automática
    Dado que clasifico la solicitud "El grifo gotea" como "mantenimiento"
    Cuando la guardo
    Entonces se notifica automáticamente al personal de mantenimiento

  @US11_E03
  Escenario: Seguimiento de estado
    Dado que una solicitud está en curso
    Cuando reviso el tablero
    Entonces veo su estado actualizado

  @US11_E04
  Escenario: Comunicación con el huésped
    Dado que la solicitud se resuelve
    Cuando actualizo el estado
    Entonces el huésped recibe una confirmación
