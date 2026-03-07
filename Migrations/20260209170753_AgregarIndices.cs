using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaCambio.Migrations
{
    /// <summary>
    /// Migration to add database indexes for improved query performance.
    /// Indexes target frequently used columns for filtering and sorting.
    /// </summary>
    public partial class AgregarIndices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═══════════════════════════════════════════════════════════
            // ÍNDICES EN TABLA OPERACIONES
            // ═══════════════════════════════════════════════════════════
            
            // Índice en Operaciones.Fecha (para filtrar por rango de fechas)
            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_Fecha",
                table: "operaciones",
                column: "fecha");

            // Índice en Operaciones.TipoOperacion (para filtrar compras/ventas)
            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_TipoOperacion",
                table: "operaciones",
                column: "tipo_operacion");

            // Índice compuesto en Operaciones (Fecha + TipoOperacion)
            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_Fecha_Tipo",
                table: "operaciones",
                columns: new[] { "fecha", "tipo_operacion" });

            // Índice en ClienteId (para buscar operaciones de un cliente)
            migrationBuilder.CreateIndex(
                name: "IX_Operaciones_ClienteId",
                table: "operaciones",
                column: "cliente_id");

            // ═══════════════════════════════════════════════════════════
            // ÍNDICES EN TABLA MOVIMIENTOS
            // ═══════════════════════════════════════════════════════════

            // Índice en Movimientos.CuentaId (para obtener movimientos de una cuenta)
            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_CuentaId",
                table: "movimientos",
                column: "cuenta_id");

            // Índice en Movimientos.OperacionId
            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_OperacionId",
                table: "movimientos",
                column: "operacion_id");

            // Índice en Movimientos.Fecha
            migrationBuilder.CreateIndex(
                name: "IX_Movimientos_Fecha",
                table: "movimientos",
                column: "fecha");

            // ═══════════════════════════════════════════════════════════
            // ÍNDICES EN TABLA ARQUEOS
            // ═══════════════════════════════════════════════════════════

            // Índice en Arqueos.Fecha
            migrationBuilder.CreateIndex(
                name: "IX_Arqueos_Fecha",
                table: "arqueos",
                column: "fecha");

            // Índice en Arqueos.CuentaId
            migrationBuilder.CreateIndex(
                name: "IX_Arqueos_CuentaId",
                table: "arqueos",
                column: "cuenta_id");

            // ═══════════════════════════════════════════════════════════
            // ÍNDICES EN TABLA AUDIT_LOGS
            // ═══════════════════════════════════════════════════════════

            // Índice en AuditLog.Fecha (para búsquedas de auditoría)
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Fecha",
                table: "audit_logs",
                column: "fecha");

            // Índice compuesto en Entidad + EntidadId
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entidad_EntidadId",
                table: "audit_logs",
                columns: new[] { "entidad", "entidad_id" });

            // ═══════════════════════════════════════════════════════════
            // ÍNDICES EN TABLA COTIZACIONES_DIARIAS
            // ═══════════════════════════════════════════════════════════

            // Índice en CotizacionesDiarias.Fecha
            migrationBuilder.CreateIndex(
                name: "IX_CotizacionesDiarias_Fecha",
                table: "cotizaciones_diarias",
                column: "fecha");

            // Índice compuesto en CotizacionesDiarias (MonedaId + Fecha)
            migrationBuilder.CreateIndex(
                name: "IX_CotizacionesDiarias_Moneda_Fecha",
                table: "cotizaciones_diarias",
                columns: new[] { "moneda_id", "fecha" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Operaciones
            migrationBuilder.DropIndex(name: "IX_Operaciones_Fecha", table: "operaciones");
            migrationBuilder.DropIndex(name: "IX_Operaciones_TipoOperacion", table: "operaciones");
            migrationBuilder.DropIndex(name: "IX_Operaciones_Fecha_Tipo", table: "operaciones");
            migrationBuilder.DropIndex(name: "IX_Operaciones_ClienteId", table: "operaciones");
            
            // Movimientos
            migrationBuilder.DropIndex(name: "IX_Movimientos_CuentaId", table: "movimientos");
            migrationBuilder.DropIndex(name: "IX_Movimientos_OperacionId", table: "movimientos");
            migrationBuilder.DropIndex(name: "IX_Movimientos_Fecha", table: "movimientos");
            
            // Arqueos
            migrationBuilder.DropIndex(name: "IX_Arqueos_Fecha", table: "arqueos");
            migrationBuilder.DropIndex(name: "IX_Arqueos_CuentaId", table: "arqueos");
            
            // AuditLogs
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Fecha", table: "audit_logs");
            migrationBuilder.DropIndex(name: "IX_AuditLogs_Entidad_EntidadId", table: "audit_logs");
            
            // CotizacionesDiarias
            migrationBuilder.DropIndex(name: "IX_CotizacionesDiarias_Fecha", table: "cotizaciones_diarias");
            migrationBuilder.DropIndex(name: "IX_CotizacionesDiarias_Moneda_Fecha", table: "cotizaciones_diarias");
        }
    }
}
