# language: es
@Sprint4 @US33
Característica: US-33 — Catálogo de servicios para el huésped
  Como huésped
  quiero ver los servicios que ofrece el hotel con sus precios y detalles
  para pedirlos desde la app sabiendo cuánto cuestan y cuánto tardan.
  Ejecutados con Reqnroll contra los casos de uso y repositorios reales del servicio Accommodations (base de datos en
  memoria): el catálogo vive en el servidor y la solicitud guarda el servicio y su precio.

  Antecedentes:
    Dado un hotel con la habitación 101
    Y el huésped "Luis" se aloja en la habitación 101
    Y el hotel ofrece los servicios:
      | servicio                 | área      | precio | minutos | disponible |
      | Desayuno a la habitación | recepción | 35.00  | 30      | sí         |
      | Toallas extra            | limpieza  | 0      | 15      | sí         |
      | Lavandería express       | limpieza  | 25.00  |         | no         |

  @US33_E01
  Escenario: Consulta del catálogo
    Cuando el huésped abre el catálogo de servicios
    Entonces ve "Desayuno a la habitación" con precio 35.00 y "Toallas extra" incluido
    Y no ve "Lavandería express"

  @US33_E02
  Escenario: Solicitud de un servicio del catálogo
    Cuando el huésped solicita el servicio "Desayuno a la habitación"
    Entonces la solicitud queda registrada como "Desayuno a la habitación" con precio 35.00
    Y llega al área de recepción con atención estimada en 30 minutos

  @US33_E03
  Escenario: Servicio no disponible
    Cuando el huésped intenta solicitar el servicio "Lavandería express"
    Entonces el sistema le indica que el servicio no está disponible

  @US33_E04
  Escenario: El precio de una solicitud no cambia con el catálogo
    Dado que el huésped solicitó el servicio "Desayuno a la habitación"
    Cuando el hotel cambia el precio de "Desayuno a la habitación" a 40.00
    Entonces la solicitud conserva el precio 35.00
