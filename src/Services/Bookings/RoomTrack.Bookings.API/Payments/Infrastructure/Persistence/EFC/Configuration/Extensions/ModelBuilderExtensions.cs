using RoomTrack.Payments.Domain.Model.ValueObjects;
using RoomTrack.Payments.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace RoomTrack.Payments.Infrastructure.Persistence.EFC.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyPaymentsConfiguration(this ModelBuilder builder)
    {
        // Payment Entity
        builder.Entity<Payment>().ToTable("payments");
        builder.Entity<Payment>().HasKey(p => p.Id);
        builder.Entity<Payment>().Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<Payment>().Property(p => p.TransactionId).IsRequired().HasMaxLength(100);
        builder.Entity<Payment>().Property(p => p.Method).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Entity<Payment>().Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Entity<Payment>().Property(p => p.OperationNumber).HasMaxLength(Payment.MaxOperationNumberLength);
        builder.Entity<Payment>().Property(p => p.Note).HasMaxLength(Payment.MaxNoteLength);
        builder.Entity<Payment>().Property(p => p.FailureReason).HasMaxLength(200);
        builder.Entity<Payment>().HasIndex(p => p.BookingId);
        builder.Entity<Payment>().Ignore(p => p.DomainEvents);
        
        builder.Entity<Payment>().Property(p => p.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        ApplyFolioConfiguration(builder);
        ApplyReceiptConfiguration(builder);
    }

    /// <summary>Folios of the stays (US-16): charges, payments, deposit and invoice data. The booking is a logical reference.</summary>
    private static void ApplyFolioConfiguration(ModelBuilder builder)
    {
        builder.Entity<GuestFolio>().ToTable("guest_folios");
        builder.Entity<GuestFolio>().HasKey(f => f.Id);
        builder.Entity<GuestFolio>().Property(f => f.Id).ValueGeneratedOnAdd();
        builder.Entity<GuestFolio>().HasIndex(f => f.BookingId).IsUnique();
        builder.Entity<GuestFolio>().HasIndex(f => f.HotelId);
        builder.Entity<GuestFolio>().Property(f => f.BookingCode).HasMaxLength(20).IsRequired();
        builder.Entity<GuestFolio>().Property(f => f.GuestName).HasMaxLength(GuestFolio.MaxGuestNameLength).IsRequired();
        builder.Entity<GuestFolio>().Property(f => f.GuestEmail).HasMaxLength(GuestFolio.MaxEmailLength).IsRequired();
        builder.Entity<GuestFolio>().Property(f => f.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<GuestFolio>().Property(f => f.DepositStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<GuestFolio>().Property(f => f.DepositMethod).HasConversion<string>().HasMaxLength(30);
        builder.Entity<GuestFolio>().Property(f => f.DepositAmount).HasColumnType("decimal(18,2)");
        builder.Entity<GuestFolio>().Property(f => f.DepositOperationNumber).HasMaxLength(Payment.MaxOperationNumberLength);
        builder.Entity<GuestFolio>().OwnsOne(f => f.InvoiceData, invoice =>
        {
            invoice.Property(i => i.Ruc).HasColumnName("invoice_ruc").HasMaxLength(11);
            invoice.Property(i => i.BusinessName).HasColumnName("invoice_business_name").HasMaxLength(InvoiceData.MaxBusinessNameLength);
            invoice.Property(i => i.Address).HasColumnName("invoice_address").HasMaxLength(InvoiceData.MaxAddressLength);
        });
        builder.Entity<GuestFolio>().Ignore(f => f.ChargesTotal);
        builder.Entity<GuestFolio>().Ignore(f => f.PaidTotal);
        builder.Entity<GuestFolio>().Ignore(f => f.Balance);
        builder.Entity<GuestFolio>().Ignore(f => f.DomainEvents);

        builder.Entity<GuestFolio>().HasMany(f => f.Charges).WithOne().HasForeignKey(c => c.GuestFolioId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<GuestFolio>().Navigation(f => f.Charges).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_charges");
        builder.Entity<FolioCharge>().ToTable("folio_charges");
        builder.Entity<FolioCharge>().HasKey(c => c.Id);
        builder.Entity<FolioCharge>().Property(c => c.Id).ValueGeneratedOnAdd();
        builder.Entity<FolioCharge>().Property(c => c.Description).HasMaxLength(FolioCharge.MaxDescriptionLength).IsRequired();
        builder.Entity<FolioCharge>().Property(c => c.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<FolioCharge>().Property(c => c.Amount).HasColumnType("decimal(18,2)");

        builder.Entity<GuestFolio>().HasMany(f => f.Payments).WithOne().HasForeignKey(p => p.GuestFolioId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<GuestFolio>().Navigation(f => f.Payments).UsePropertyAccessMode(PropertyAccessMode.Field).HasField("_payments");
        builder.Entity<FolioPayment>().ToTable("folio_payments");
        builder.Entity<FolioPayment>().HasKey(p => p.Id);
        builder.Entity<FolioPayment>().Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Entity<FolioPayment>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<FolioPayment>().Property(p => p.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Entity<FolioPayment>().Property(p => p.OperationNumber).HasMaxLength(Payment.MaxOperationNumberLength);
    }

    /// <summary>Receipts of the payments (US-17); the number is unique per hotel and series.</summary>
    private static void ApplyReceiptConfiguration(ModelBuilder builder)
    {
        builder.Entity<Receipt>().ToTable("receipts");
        builder.Entity<Receipt>().HasKey(r => r.Id);
        builder.Entity<Receipt>().Property(r => r.Id).ValueGeneratedOnAdd();
        builder.Entity<Receipt>().HasIndex(r => new { r.HotelId, r.Series, r.Number }).IsUnique();
        builder.Entity<Receipt>().HasIndex(r => new { r.Source, r.SourceId }).IsUnique();
        builder.Entity<Receipt>().HasIndex(r => r.BookingId);
        builder.Entity<Receipt>().HasIndex(r => new { r.HotelId, r.IssuedAt });
        builder.Entity<Receipt>().Property(r => r.BookingCode).HasMaxLength(20).IsRequired();
        builder.Entity<Receipt>().Property(r => r.Type).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Entity<Receipt>().Property(r => r.Series).HasMaxLength(4).IsRequired();
        builder.Entity<Receipt>().Property(r => r.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Entity<Receipt>().Property(r => r.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Entity<Receipt>().Property(r => r.Total).HasColumnType("decimal(18,2)");
        builder.Entity<Receipt>().Property(r => r.Subtotal).HasColumnType("decimal(18,2)");
        builder.Entity<Receipt>().Property(r => r.Igv).HasColumnType("decimal(18,2)");
        builder.Entity<Receipt>().Property(r => r.Concept).HasMaxLength(Receipt.MaxConceptLength).IsRequired();
        builder.Entity<Receipt>().Property(r => r.CustomerName).HasMaxLength(InvoiceData.MaxBusinessNameLength).IsRequired();
        builder.Entity<Receipt>().Property(r => r.CustomerDocument).HasMaxLength(11);
        builder.Entity<Receipt>().Property(r => r.CustomerAddress).HasMaxLength(InvoiceData.MaxAddressLength);
        builder.Entity<Receipt>().Property(r => r.CustomerEmail).HasMaxLength(GuestFolio.MaxEmailLength).IsRequired();
        builder.Entity<Receipt>().Ignore(r => r.FullNumber);
    }
}