using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReposteriaApi.Modelo;

// Cada clase es una tabla de Oracle. Los nombres de tabla y columna están en MAYÚSCULAS
// para que coincidan con sql/01_esquema.sql (Oracle guarda en mayúsculas lo que no va entre comillas).

public interface IEntidad
{
    int Id { get; set; }
}

[Table("INSUMOS")]
public class Insumo : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("CODIGO"), MaxLength(30)] public string Codigo { get; set; } = "";
    [Column("NOMBRE"), MaxLength(80)] public string Nombre { get; set; } = "";
    [Column("UNIDAD"), MaxLength(20)] public string Unidad { get; set; } = "";
    /// <summary>Precio de la lista base con la que se cotizaron los pedidos de ejemplo.</summary>
    [Column("PRECIO_COTIZADO", TypeName = "NUMBER(12,2)")] public decimal PrecioCotizado { get; set; }
    /// <summary>Precio de hoy; null si todavía no se ha registrado.</summary>
    [Column("PRECIO_ACTUAL", TypeName = "NUMBER(12,2)")] public decimal? PrecioActual { get; set; }
}

[Table("RECETAS")]
public class Receta : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("CODIGO"), MaxLength(30)] public string Codigo { get; set; } = "";
    [Column("NOMBRE"), MaxLength(80)] public string Nombre { get; set; } = "";
    /// <summary>Para cuántas personas (o unidades) está pensada la receta.</summary>
    [Column("TAMANO_BASE")] public int TamanoBase { get; set; }
    [Column("MANO_OBRA", TypeName = "NUMBER(12,2)")] public decimal ManoObra { get; set; }
    public List<RecetaInsumo> Insumos { get; set; } = [];
}

[Table("RECETA_INSUMOS")]
public class RecetaInsumo : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("RECETA_ID")] public int RecetaId { get; set; }
    [Column("INSUMO_ID")] public int InsumoId { get; set; }
    [Column("CANTIDAD", TypeName = "NUMBER(10,3)")] public decimal Cantidad { get; set; }
}

[Table("CAPACIDADES")]
public class Capacidad : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    /// <summary>horno, decoracion o personal.</summary>
    [Column("RECURSO"), MaxLength(20)] public string Recurso { get; set; } = "";
    [Column("HORAS_DIA", TypeName = "NUMBER(6,2)")] public decimal HorasDia { get; set; }
}

[Table("HISTORIAL_MENSUAL")]
public class HistorialMensual : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("ANIO")] public int Anio { get; set; }
    [Column("MES")] public int Mes { get; set; }
    [Column("INGRESOS", TypeName = "NUMBER(14,2)")] public decimal Ingresos { get; set; }
    [Column("COSTOS", TypeName = "NUMBER(14,2)")] public decimal Costos { get; set; }
    [Column("NUM_PEDIDOS")] public int NumPedidos { get; set; }
}

[Table("SOLICITUDES_NLP")]
public class SolicitudNlp : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("TEXTO"), MaxLength(1000)] public string Texto { get; set; } = "";
    /// <summary>Lo que el interpretador entendió (sabor, tamaño, fecha, diseño), en JSON.</summary>
    [Column("RESULTADO_JSON", TypeName = "CLOB")] public string? ResultadoJson { get; set; }
    [Column("CREADO_EN", TypeName = "TIMESTAMP")] public DateTime CreadoEn { get; set; }
}

[Table("PEDIDOS")]
public class Pedido : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("CODIGO"), MaxLength(20)] public string Codigo { get; set; } = "";
    [Column("CLIENTE"), MaxLength(120)] public string Cliente { get; set; } = "";
    [Column("PRODUCTO"), MaxLength(200)] public string Producto { get; set; } = "";
    [Column("RECETA_ID")] public int RecetaId { get; set; }
    [Column("COMPLEJIDAD"), MaxLength(10)] public string Complejidad { get; set; } = "";
    [Column("FECHA", TypeName = "DATE")] public DateTime Fecha { get; set; }
    [Column("ESTADO"), MaxLength(30)] public string Estado { get; set; } = "";
    [Column("PRECIO", TypeName = "NUMBER(12,2)")] public decimal Precio { get; set; }
    [Column("ORIGEN"), MaxLength(10)] public string Origen { get; set; } = "";
    /// <summary>1 = tamaño de la receta; 2 = el doble.</summary>
    [Column("ESCALA", TypeName = "NUMBER(6,2)")] public decimal Escala { get; set; } = 1;
    [Column("CREADO_EN", TypeName = "TIMESTAMP")] public DateTime CreadoEn { get; set; }
    [Column("SOLICITUD_ID")] public int? SolicitudId { get; set; }
    /// <summary>Precio de cada insumo en el momento en que se cotizó ESTE pedido.</summary>
    public List<PedidoPrecio> PreciosCotizados { get; set; } = [];
}

[Table("PEDIDO_PRECIOS")]
public class PedidoPrecio : IEntidad
{
    [Key, Column("ID")] public int Id { get; set; }
    [Column("PEDIDO_ID")] public int PedidoId { get; set; }
    [Column("INSUMO_ID")] public int InsumoId { get; set; }
    [Column("PRECIO", TypeName = "NUMBER(12,2)")] public decimal Precio { get; set; }
}
