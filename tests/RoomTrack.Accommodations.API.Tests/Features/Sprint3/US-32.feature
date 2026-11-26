# language: es
@Sprint3 @US32
Característica: US-32 — Entrega de turno
  Como miembro del Staff Operativo
  quiero dejar un resumen de pendientes al finalizar mi turno
  para que el siguiente colaborador continúe sin pérdida de información.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la habitación 101 y la habitación 102
    Y "Ana" trabaja en limpieza
    Y "Rosa" trabaja en limpieza
    Y "Ana" tiene abierta la limpieza de la habitación 101
    Y "Ana" reportó la incidencia "Persiana trabada" en la habitación 102

  @US32_E01
  Escenario: Registro de pendientes
    Dado que termino mi turno
    Cuando completo el reporte de entrega
    Entonces quedan registradas las tareas e incidencias abiertas

  @US32_E02
  Escenario: Visualización por el siguiente turno
    Dado que inicio un nuevo turno
    Cuando accedo a mi panel
    Entonces veo el resumen dejado por el turno anterior

  @US32_E03
  Escenario: Confirmación de lectura
    Dado que revisé la entrega de turno
    Cuando la marco como leída
    Entonces queda registrado quién y cuándo lo confirmó
