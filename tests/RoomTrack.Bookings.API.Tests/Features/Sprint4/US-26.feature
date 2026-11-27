# language: es
@Sprint4 @US26
Característica: US-26 — Satisfacción del huésped por área
  Como administrador del hotel
  quiero ver los puntajes de satisfacción de los huéspedes agrupados por área
  para saber qué área necesita mejorar.
  Ejecutados con Reqnroll contra los casos de uso y repositorios reales del contexto Bookings (base de datos en
  memoria): los puntajes salen de las evaluaciones de las estadías (US-21).

  @US26_E01
  Escenario: Puntajes por área
    Dado que los huéspedes evaluaron sus estadías:
      | general | recepción | limpieza | mantenimiento |
      | 5       | 5         | 4        | 2             |
      | 4       | 5         | 5        | 3             |
      | 3       | 4         |          | 2             |
    Cuando consulto la satisfacción por área
    Entonces la satisfacción es:
      | área          | evaluaciones | promedio | satisfechos |
      | general       | 3            | 4        | 66.7        |
      | recepción     | 3            | 4.67     | 100         |
      | limpieza      | 2            | 4.5      | 100         |
      | mantenimiento | 3            | 2.33     | 0           |

  @US26_E02
  Escenario: Solo cuentan las evaluaciones del periodo
    Dado que un huésped evaluó su estadía hace 40 días con 1 estrella
    Y que los huéspedes evaluaron sus estadías:
      | general | recepción | limpieza | mantenimiento |
      | 5       | 5         | 5        | 5             |
    Cuando consulto la satisfacción por área
    Entonces la satisfacción general tiene 1 evaluación con promedio 5

  @US26_E03
  Escenario: Sin evaluaciones en el periodo
    Cuando consulto la satisfacción por área
    Entonces cada área aparece sin evaluaciones ni promedio
