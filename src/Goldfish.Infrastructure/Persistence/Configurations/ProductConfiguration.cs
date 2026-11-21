using Goldfish.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Goldfish.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasMaxLength(Product.NameMaxLength)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Property(p => p.CreatedOnUtc)
            .HasColumnType("datetime2")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
