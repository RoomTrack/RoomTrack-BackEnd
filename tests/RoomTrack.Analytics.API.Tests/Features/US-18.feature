# language: es
@Sprint4 @US18
Característica: US-18 — Indicadores clave del panel del administrador
  Como administrador
  quiero ver los indicadores clave del hotel en un panel
  para supervisar la operación diaria de forma eficiente.
  El servicio Analytics arma su modelo de lectura con los eventos de integración de Bookings y Accommodations
  (RabbitMQ). Los escenarios publican esos eventos a los consumidores reales y consultan el panel del mes.

  Antecedentes:
    Dado que el hotel 1 tiene 4 habitaciones registradas

  @US18_E01
  Escenario: Ingresos del mes
    Dado que en el hotel 1 se completaron pagos de 200.00 y 150.50 este mes
    Y se reembolsó un pago de 999.00
    Y el hotel 2 cobró 80.00
    Cuando abro el panel del hotel 1
    Entonces veo ingresos por 350.50

  @US18_E02
  Escenario: Reservas, cancelaciones y ocupación
    Dado que el hotel 1 tiene este mes 2 reservas confirmadas, 1 en estancia y 1 cancelada
    Cuando abro el panel del hotel 1
    Entonces veo 4 reservas, 1 cancelada y una ocupación de 75 %

  @US18_E03
  Escenario: Una habitación retirada no cuenta para la ocupación
    Dado que el hotel 1 tiene este mes 2 reservas confirmadas, 0 en estancia y 0 cancelada
    Y se retira una habitación del hotel 1
    Cuando abro el panel del hotel 1
    Entonces la ocupación es de 66.67 %

  @US18_E04
  Escenario: Vista de toda la cadena
    Dado que en el hotel 1 se completaron pagos de 200.00 y 150.50 este mes
    Y el hotel 2 cobró 80.00
    Cuando abro el panel de toda la cadena
    Entonces veo ingresos por 430.50
