# language: es
@Sprint2 @US17
Característica: US-17 — Comprobantes y facturación
  Criterios de aceptación del informe (Avance 3, corte 01/11/2026).
  Los escenarios con @ignore aún no tienen implementación en la API (ver README.md): Reqnroll los reporta como
  omitidos, no como aprobados.

  @US17_E01 @pendiente_implementacion @ignore
  Escenario: Emisión automática de comprobante
    # Requiere la emisión de comprobantes, que la API aún no tiene.
    Dado que un huésped completa un pago
    Cuando se procesa
    Entonces el sistema genera automáticamente el comprobante correspondiente

  @US17_E02 @pendiente_implementacion @ignore
  Escenario: Solicitud de factura con datos fiscales
    # Requiere la emisión de comprobantes, que la API aún no tiene.
    Dado que un huésped requiere factura
    Cuando ingresa sus datos fiscales antes del check-out
    Entonces el sistema genera el documento con esa información

  @US17_E03 @pendiente_implementacion @ignore
  Escenario: Reenvío de comprobante
    # Requiere la emisión de comprobantes, que la API aún no tiene.
    Dado que un huésped no recibió su comprobante
    Cuando lo reenvío desde el panel
    Entonces le llega nuevamente a su correo registrado

  @US17_E04 @pendiente_implementacion @ignore
  Escenario: Historial de pagos
    # Requiere la emisión de comprobantes, que la API aún no tiene.
    Dado que necesito revisar transacciones pasadas
    Cuando accedo al historial de pagos
    Entonces veo todos los cobros, reembolsos y comprobantes emitidos por fecha
