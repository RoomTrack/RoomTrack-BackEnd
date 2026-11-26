# language: es
@Sprint3 @US31
Característica: US-31 — Gestión de turnos del personal
  Como administrador
  quiero definir y asignar los turnos del personal operativo
  para asegurar cobertura adecuada en cada horario.
  Criterios de aceptación del informe (TF1, corte 25/11/2026), ejecutados con Reqnroll contra el servicio de turnos y los
  perfiles de personal reales del servicio Profiles (base de datos en memoria).

  Antecedentes:
    Dado que el hotel tiene al personal:
      | nombre | rol           |
      | Rosa   | limpieza      |
      | Ana    | limpieza      |
      | Carlos | recepción     |
      | Mario  | mantenimiento |

  @US31_E01
  Escenario: Creación de turno
    Dado que defino el turno "mañana" de "limpieza" del 01/12/2030 para 2 personas
    Cuando asigno a "Rosa" y "Ana"
    Entonces cada una ve su horario en su panel

  @US31_E02
  Escenario: Detección de falta de cobertura
    Dado que el turno "noche" de "recepción" del 01/12/2030 necesita 1 persona y no tiene a nadie
    Y el turno "mañana" de "limpieza" del 01/12/2030 necesita 3 personas y tiene a "Rosa"
    Cuando reviso la planificación
    Entonces el sistema resalta el hueco de cobertura de "recepción" primero
    Y señala que a "limpieza" le faltan 2 personas

  @US31_E03
  Escenario: Cambio de turno
    Dado que "Rosa" trabaja el turno "mañana" de "limpieza" del 01/12/2030
    Y "Ana" trabaja el turno "tarde" de "limpieza" del 01/12/2030
    Y "Rosa" solicita cambiar su turno con "Ana"
    Cuando lo apruebo
    Entonces el calendario se actualiza para ambas partes involucradas

  @US31_E04
  Escenario: No se asigna personal de otro hotel
    Dado que defino el turno "mañana" de "limpieza" del 01/12/2030 para 2 personas
    Cuando intento asignar a alguien que no trabaja en el hotel
    Entonces el sistema rechaza la operación
