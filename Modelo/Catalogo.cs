namespace ReposteriaApi.Modelo;

/// <summary>Reglas de negocio fijas: recursos, complejidades, estados y cuánto gasta cada pedido.</summary>
public static class Catalogo
{
    public const string Pendiente = "Pendiente de aprobación";
    public const string EnProduccion = "En producción";
    public const string Listo = "Listo para entrega";
    public const string Entregado = "Entregado";
    public const string Cancelado = "Cancelado";

    public static readonly string[] Estados = [Pendiente, EnProduccion, Listo, Entregado, Cancelado];
    public static readonly string[] Complejidades = ["Baja", "Media", "Alta"];
    public static readonly string[] Recursos = ["horno", "decoracion", "personal"];

    public static readonly Dictionary<string, string> NombresRecurso = new()
    {
        ["horno"] = "Horno",
        ["decoracion"] = "Decoración",
        ["personal"] = "Personal",
    };

    /// <summary>Horas que gasta un pedido, por complejidad y por recurso.</summary>
    public static readonly Dictionary<string, Dictionary<string, decimal>> Consumo = new()
    {
        ["Baja"] = new() { ["horno"] = 1, ["decoracion"] = 1, ["personal"] = 2 },
        ["Media"] = new() { ["horno"] = 2, ["decoracion"] = 2, ["personal"] = 3 },
        ["Alta"] = new() { ["horno"] = 3, ["decoracion"] = 4, ["personal"] = 4 },
    };

    /// <summary>Horas por día de cada recurso si la tabla CAPACIDADES está vacía.</summary>
    public static readonly Dictionary<string, decimal> CapacidadPorDefecto = new()
    {
        ["horno"] = 8,
        ["decoracion"] = 6,
        ["personal"] = 10,
    };

    /// <summary>Margen de ganancia con el que se sugiere el precio de un pedido nuevo.</summary>
    public const decimal MargenObjetivo = 0.4m;

    /// <summary>Cuántos días hacia adelante mira la agenda.</summary>
    public const int HorizonteDias = 14;
}
