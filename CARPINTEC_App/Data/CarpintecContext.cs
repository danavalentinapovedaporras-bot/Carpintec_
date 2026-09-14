using System;
using System.Collections.Generic;
using CARPINTEC_App.Models;
using Microsoft.EntityFrameworkCore;

namespace CARPINTEC_App.Data;

public partial class CarpintecContext : DbContext
{
    public CarpintecContext()
    {
    }


    public CarpintecContext(DbContextOptions<CarpintecContext> options)
        : base(options)
    {
    }

    public DbSet<Factura> Facturas { get; set; }

    public DbSet<DetalleFactura> DetalleFacturas { get; set; }
    public virtual DbSet<Cliente> Clientes { get; set; }
    public virtual DbSet<Configuracion> Configuracions { get; set; }
    public virtual DbSet<Cotizacion> Cotizaciones { get; set; }
    public virtual DbSet<DetalleCotizacion> DetalleCotizacion { get; set; }
    public virtual DbSet<DetallePedido> DetallePedidos { get; set; }
    public virtual DbSet<Empleado> Empleados { get; set; }
    public virtual DbSet<Inventario> Inventarios { get; set; }
    public virtual DbSet<ManoObra> ManoObras { get; set; }
    
    public virtual DbSet<Pedido> Pedidos { get; set; }
    public virtual DbSet<Pqr> Pqrs { get; set; }
    public virtual DbSet<Producto> Productos { get; set; }
    public virtual DbSet<ActividadTaller> ActividadTallers { get; set; }
    public virtual DbSet<Usuario> Usuarios { get; set; }
    public virtual DbSet<Venta> Ventas { get; set; }
    public virtual DbSet<Inventario> VistaInventario { get; set; }
    public DbSet<SolicitudReposicion> SolicitudesReposicion { get; set; }
    public DbSet<MovimientoInventario> MovimientosInventario { get; set; }

    public virtual DbSet<ChatBot> ChatBots { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Carpintec;Trusted_Connection=True;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.IdCliente).HasName("PK__Cliente__D5946642C86CE765");
            entity.ToTable("Cliente");
            entity.HasIndex(e => e.Correo, "UQ__Cliente__60695A19BDB47717").IsUnique();
            entity.HasIndex(e => e.Documento, "UQ__Cliente__AF73706D05F912EA").IsUnique();

            entity.Property(e => e.Apellido).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Ciudad).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Contacto).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Direccion).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.Documento).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Activo");
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.NombreEmpresa).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TipoCliente).HasMaxLength(20).IsUnicode(false);
           
        });

        modelBuilder.Entity<ChatBot>(entity =>
{
    entity.HasKey(e => e.IdChat);

    entity.ToTable("ChatBot");

    entity.Property(e => e.MensajeUsuario)
        .HasMaxLength(500)
        .IsUnicode(false);

    entity.Property(e => e.RespuestaBot)
        .HasMaxLength(500)
        .IsUnicode(false);

    entity.Property(e => e.Fecha)
        .HasDefaultValueSql("(getdate())")
        .HasColumnType("datetime");


    entity.HasOne(e => e.Usuario)
        .WithMany()
        .HasForeignKey(e => e.IdUsuario)
        .HasConstraintName("FK_ChatBot_Usuario");
});

        modelBuilder.Entity<Configuracion>(entity =>
        {
            entity.HasKey(e => e.IdConfiguracion).HasName("PK__Configur__F6E145D0C684503B");
            entity.ToTable("Configuracion");

            entity.Property(e => e.Ciudad).HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Direccion).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Iva).HasColumnType("decimal(5, 2)").HasColumnName("IVA");
            entity.Property(e => e.Logo).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Moneda).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Nit).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.NombreEmpresa).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.SitioWeb).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false);
        });

        modelBuilder.Entity<Cotizacion>(entity =>
        {
            entity.HasKey(e => e.IdCotizacion).HasName("PK__Cotizaci__9A6DA9EF5EE368DE");
            entity.ToTable("Cotizacion");
            entity.HasIndex(e => e.Folio, "UQ__Cotizaci__BAB84EF7BAE0DA5F").IsUnique();

            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Pendiente");
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Folio).HasMaxLength(20).IsUnicode(false);
          
            entity.Property(e => e.Total).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.Cotizacions)
                .HasForeignKey(d => d.IdCliente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cotizacion_Cliente");

            entity.HasOne(d => d.IdEmpleadoNavigation).WithMany(p => p.Cotizacions)
                .HasForeignKey(d => d.IdEmpleado)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cotizacion_Empleado");
        });

        modelBuilder.Entity<DetalleCotizacion>(entity =>
        {
            entity.HasKey(e => e.IdDetalleCotizacion).HasName("PK__DetalleC__95224779BF6BC79F");
            entity.ToTable("DetalleCotizacion");

            entity.Property(e => e.Medidas).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.IdCotizacionNavigation).WithMany(p => p.DetalleCotizacions)
                .HasForeignKey(d => d.IdCotizacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetalleCotizacion_Cotizacion");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.DetalleCotizacions)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetalleCotizacion_Producto");
        });

        modelBuilder.Entity<DetallePedido>(entity =>
        {
            entity.HasKey(e => e.IdDetallePedido).HasName("PK__DetalleP__48AFFD957641FA2C");
            entity.ToTable("DetallePedido");

            entity.Property(e => e.Alto).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.Ancho).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.Material).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Observaciones).HasMaxLength(250).IsUnicode(false);
            entity.Property(e => e.PrecioUnitario).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Prioridad).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Profundidad).HasColumnType("decimal(8, 2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.IdPedidoNavigation).WithMany(p => p.DetallePedidos)
                .HasForeignKey(d => d.IdPedido)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetallePedido_Pedido");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.DetallePedidos)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DetallePedido_Producto");
        });

        modelBuilder.Entity<Empleado>(entity =>
        {
            entity.HasKey(e => e.IdEmpleado).HasName("PK__Empleado__CE6D8B9EDBB7125C");
            entity.ToTable("Empleado");
            entity.HasIndex(e => e.Correo, "UQ__Empleado__60695A199165EE67").IsUnique();
            entity.HasIndex(e => e.Documento, "UQ__Empleado__AF73706D8B34BE79").IsUnique();

            entity.Property(e => e.Apellido).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Cargo).HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Direccion).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.Documento).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Foto).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Salario).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Telefono).HasMaxLength(20).IsUnicode(false);

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Empleados)
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_Empleado_Usuario");
        });

        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.HasKey(e => e.IdInventario).HasName("PK__Inventar__1927B20CD2C7EB0F");
            entity.ToTable("Inventario");

            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Disponible");
            entity.Property(e => e.FechaActualizacion).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.PrecioCompra).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Ubicacion).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.UnidadMedida).HasMaxLength(30).IsUnicode(false);

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.Inventarios)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Inventario_Producto");
        });

        modelBuilder.Entity<ManoObra>(entity =>
        {
            entity.HasKey(e => e.IdManoObra).HasName("PK__ManoObra__5F63F37FC8AB9D6B");
            entity.ToTable("ManoObra");
            entity.HasIndex(e => e.CodigoOrden, "UQ__ManoObra__1B9107A45067F56E").IsUnique();

            entity.Property(e => e.CodigoOrden).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.CostoHora).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.HorasEstimadas).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.HorasReales).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Observaciones).HasMaxLength(300).IsUnicode(false);
            entity.Property(e => e.Proceso).HasMaxLength(100).IsUnicode(false);

            entity.HasOne(d => d.IdEmpleadoNavigation).WithMany(p => p.ManoObras)
                .HasForeignKey(d => d.IdEmpleado)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ManoObra_Empleado");

            entity.HasOne(d => d.IdPedidoNavigation).WithMany(p => p.ManoObras)
                .HasForeignKey(d => d.IdPedido)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ManoObra_Pedido");

            entity.HasOne(d => d.IdProductoNavigation).WithMany(p => p.ManoObras)
                .HasForeignKey(d => d.IdProducto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ManoObra_Producto");
        });
        modelBuilder.Entity<Factura>(entity =>
        {
            entity.HasKey(e => e.IdFactura);

            entity.ToTable("Factura");

            entity.Property(e => e.Folio)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.Cliente)
                .HasMaxLength(150)
                .IsUnicode(false);

            entity.Property(e => e.Fecha)
                .HasColumnType("date");

            entity.Property(e => e.Total)
                .HasColumnType("decimal(12,2)");

            entity.Property(e => e.Estado)
                .HasMaxLength(20)
                .IsUnicode(false);
        });
        modelBuilder.Entity<DetalleFactura>(entity =>
        {
            entity.HasKey(e => e.IdDetalleFactura);

            entity.ToTable("DetalleFactura");

            entity.Property(e => e.PrecioUnitario)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Subtotal)
                  .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Observacion)
                  .HasMaxLength(200)
                  .IsUnicode(false);

            entity.HasOne(d => d.Factura)
      .WithMany(f => f.DetallesFactura)
                  .HasForeignKey(d => d.IdFactura)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DetalleFactura_Factura");

            entity.HasOne(d => d.Producto)
                  .WithMany()
                  .HasForeignKey(d => d.IdProducto)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_DetalleFactura_Producto");
        });
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.HasKey(e => e.IdPedido).HasName("PK__Pedido__9D335DC38C87F4A5");
            entity.ToTable("Pedido");
            entity.HasIndex(e => e.CodigoPedido, "UQ__Pedido__72162F0A956DFD52").IsUnique();

            entity.Property(e => e.CodigoPedido).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Pendiente");
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Observaciones).HasMaxLength(300).IsUnicode(false);
            entity.Property(e => e.ValorTotal).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.IdCliente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pedido_Cliente");

            entity.HasOne(d => d.IdCotizacionNavigation).WithMany(p => p.Pedidos)
                .HasForeignKey(d => d.IdCotizacion)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pedido_Cotizacion");
        });

        modelBuilder.Entity<Pqr>(entity =>
        {
            entity.HasKey(e => e.IdPqr).HasName("PK__PQR__2ACC401AAB2402EC");
            entity.ToTable("PQR");
            entity.HasIndex(e => e.CodigoPqr, "UQ__PQR__F29491639156D5E6").IsUnique();

            entity.Property(e => e.IdPqr).HasColumnName("IdPQR");
            entity.Property(e => e.Asunto).HasMaxLength(150).IsUnicode(false);
            entity.Property(e => e.CodigoPqr).HasMaxLength(20).IsUnicode(false).HasColumnName("CodigoPQR");
            entity.Property(e => e.Descripcion).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Respuesta).HasMaxLength(500).IsUnicode(false);
            entity.Property(e => e.Tipo).HasMaxLength(20).IsUnicode(false);

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.Pqrs)
                .HasForeignKey(d => d.IdCliente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PQR_Cliente");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.IdProducto).HasName("PK__Producto__098892108A7CCDF4");
            entity.ToTable("Producto");
            entity.HasIndex(e => e.Codigo, "UQ__Producto__06370DAC5FF941F8").IsUnique();

            entity.Property(e => e.Categoria).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Codigo).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Descripcion).HasMaxLength(300).IsUnicode(false);
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false).HasDefaultValue("Activo");
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Imagen).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Material).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Medidas).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Precio).HasColumnType("decimal(12, 2)");
        });

        modelBuilder.Entity<ActividadTaller>(entity =>
        {
            entity.HasKey(e => e.IdActividad).HasName("PK_ActividadTaller");

            entity.ToTable("ActividadTaller");

            entity.HasIndex(e => e.Fecha, "IX_ActividadTaller_Fecha");

            entity.Property(e => e.Categoria).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.FechaRegistro).HasDefaultValueSql("(getdate())").HasColumnType("datetime");
            entity.Property(e => e.Texto).HasMaxLength(140).IsUnicode(false);

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.ActividadTallers)
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_ActividadTaller_Usuario");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK__Usuario__5B65BF97E14CE037");
            entity.ToTable("Usuario");
            entity.HasIndex(e => e.Correo, "UQ__Usuario__60695A195E14FC4B").IsUnique();

            entity.Property(e => e.Apellido).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Contraseña).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Correo).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Nombre).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.Rol).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.IntentosFallidos)
    .HasDefaultValue(0);
        });

        modelBuilder.Entity<Venta>(entity =>
        {
            // Indícale que en SQL Server la tabla se llama exactamente "Venta"
            entity.ToTable("Venta");

            entity.HasKey(e => e.IdVenta).HasName("PK__Venta__BC1248BD5FC6DC6A");
            entity.HasIndex(e => e.NumeroFactura, "UQ__Venta__CF12F9A66A3EC81B").IsUnique();

            entity.Property(e => e.Estado).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.IVA).HasColumnType("decimal(12, 2)").HasColumnName("IVA");
            entity.Property(e => e.MetodoPago).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.NumeroFactura).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Observaciones).HasMaxLength(250).IsUnicode(false);
            entity.Property(e => e.Subtotal).HasColumnType("decimal(12, 2)");
            entity.Property(e => e.Total).HasColumnType("decimal(12, 2)");

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.Ventas)
                .HasForeignKey(d => d.IdCliente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Venta_Cliente");

            entity.HasOne(d => d.IdPedidoNavigation).WithMany(p => p.Venta)
                .HasForeignKey(d => d.IdPedido)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Venta_Pedido");
        });

        modelBuilder.Entity<Inventario>()
            .ToView("Vista_Inventario")
            .HasKey(i => i.IdInventario);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}