# language: es
@Sprint1 @US07
Característica: US-07 — Checklist de limpieza por habitación
  Como personal de limpieza
  quiero seguir una checklist estandarizada por tipo de habitación
  para asegurar que no omita ningún paso del protocolo del hotel.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra los casos de uso,
  repositorios y manejadores de eventos reales del servicio Accommodations (base de datos en memoria).

  Antecedentes:
    Dado un hotel con la suite 501 y la habitación estándar 101
    Y el hotel configuró la checklist de las suites:
      | paso                   | obligatorio |
      | Cambiar ropa de cama   | sí          |
      | Desinfectar el jacuzzi | sí          |
      | Reponer frutas         | no          |
    Y "Rosa" trabaja en limpieza

  @US07_E01
  Escenario: Checklist por tipo
    Dado que recepción creó la tarea de limpieza de la habitación 501 para "Rosa"
    Cuando "Rosa" abre la tarea
    Entonces ve la checklist específica para suites
    Y una tarea de la habitación 101 no tiene la checklist de las suites

  @US07_E02
  Escenario: Marcar ítems
    Dado que "Rosa" inició la limpieza de la habitación 501
    Cuando "Rosa" marca el paso "Cambiar ropa de cama"
    Entonces el progreso de la checklist es 33 %

  @US07_E03
  Escenario: Cierre bloqueado sin checklist completa
    Dado que "Rosa" inició la limpieza de la habitación 501
    Y "Rosa" marcó el paso "Cambiar ropa de cama"
    Cuando "Rosa" intenta marcar la habitación como lista
    Entonces el sistema rechaza la operación
    Y se señala lo pendiente: "Desinfectar el jacuzzi"
