using System.Text.Json.Serialization;

namespace Comprobante.Core.Validacion;

/// <summary>Qué tanto revisa esta app una regla de la matriz de errores del SAT. En JSON va como texto.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EstadoDeRegla>))]
public enum EstadoDeRegla
{
    /// <summary>La app revisa la regla completa.</summary>
    [JsonStringEnumMemberName("revisada")]
    Revisada,

    /// <summary>La app revisa una parte de la regla: la nota dice cuál.</summary>
    [JsonStringEnumMemberName("parcial")]
    Parcial,

    /// <summary>La app no revisa la regla: la nota dice por qué.</summary>
    [JsonStringEnumMemberName("noRevisada")]
    NoRevisada,
}

/// <summary>Una regla de la matriz de errores del SAT y hasta dónde la cubre el validador.</summary>
/// <param name="Codigo">El código del SAT: <c>CFDI40161</c>.</param>
/// <param name="Estado">Si se revisa completa, en parte o no se revisa.</param>
/// <param name="Nota">Qué se revisa exactamente o por qué no se puede revisar sin conexión.</param>
/// <param name="CubiertaPor">Si la regla comparte condición con otra y se informa con el código de esa otra.</param>
public sealed record ReglaSat(string Codigo, EstadoDeRegla Estado, string? Nota = null, string? CubiertaPor = null);

/// <summary>
/// Las 129 reglas de la matriz de errores del SAT (<c>sat/matriz/matriz-errores.csv</c>), cada una con lo que el validador
/// hace con ella. Una regla que necesita datos que la app no tiene sin conexión (la lista de RFC inscritos, el tipo de cambio
/// oficial, el timbrado del PAC) queda como no revisada y con su motivo, en lugar de aprobarse en silencio.
/// </summary>
public static class ReglasSat
{
    /// <summary>El código genérico del SAT para errores sin clasificar: la app lo usa cuando una regla falla por un error interno.</summary>
    public const string NoClasificado = "CFDI40999";

    /// <summary>Todas las reglas, en el orden de la matriz.</summary>
    public static IReadOnlyList<ReglaSat> Todas { get; } =
    [
        Revisada("CFDI40101", "Revisa el formato y que sea una fecha real. Aparte, el aviso CMP101 señala si la fecha aún no llega en el lugar de expedición."),
        Revisada("CFDI40102", "Verifica el sello con la llave pública del certificado que trae el comprobante."),
        Revisada("CFDI40103"),
        Revisada("CFDI40104"),
        Revisada("CFDI40105"),
        Revisada("CFDI40106", "Revisa que el atributo Certificado sea un archivo .cer válido."),
        Revisada("CFDI40107"),
        Revisada("CFDI40108"),
        Revisada("CFDI40109"),
        Revisada("CFDI40110"),
        Revisada("CFDI40111"),
        Revisada("CFDI40112"),
        Revisada("CFDI40113"),
        Revisada("CFDI40114"),
        Revisada("CFDI40115"),
        Revisada("CFDI40116"),
        Revisada("CFDI40117"),
        No("CFDI40118", "Compara el TipoCambio con el tipo de cambio oficial del día (DOF), que no está en los catálogos de la app."),
        Revisada("CFDI40119"),
        No("CFDI40120", "El límite del Total lo fija la Resolución Miscelánea Fiscal vigente, que la app no conoce."),
        Revisada("CFDI40121"),
        Revisada("CFDI40122"),
        Revisada("CFDI40123"),
        Revisada("CFDI40124"),
        Revisada("CFDI40125"),
        Revisada("CFDI40126"),
        No("CFDI40127", "Depende de los límites de TipoCambio y Total (CFDI40118 y CFDI40120), que la app no puede calcular."),
        No("CFDI40128", "La clave de confirmación la entrega y la verifica el PAC al timbrar."),
        No("CFDI40129", "El PAC es quien verifica que la clave de confirmación no se haya usado en otro comprobante."),
        Revisada("CFDI40130", "Compara el nombre exactamente con PUBLICO EN GENERAL."),
        Revisada("CFDI40131"),
        Revisada("CFDI40132"),
        Revisada("CFDI40133"),
        Revisada("CFDI40134"),
        Revisada("CFDI40135"),
        Revisada("CFDI40136", "El año en curso es el de la Fecha del comprobante."),
        Revisada("CFDI40137"),
        No("CFDI40138", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        No("CFDI40139", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        Revisada("CFDI40140"),
        Revisada("CFDI40141"),
        No("CFDI40142", "Requiere el servicio Validador de RFC del SAT para facturar a través de un adquirente."),
        No("CFDI40143", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        No("CFDI40144", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        No("CFDI40145", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        Revisada("CFDI40146"),
        No("CFDI40147", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        No("CFDI40148", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        Revisada("CFDI40149"),
        Revisada("CFDI40150"),
        Parcial("CFDI40151", "Revisa que solo el RFC genérico extranjero (XEXX010101000) traiga ResidenciaFiscal; la app no puede saber si un RFC está inscrito en el SAT."),
        Revisada("CFDI40152"),
        Revisada("CFDI40153"),
        Parcial("CFDI40154", "Revisa que solo el RFC genérico extranjero (XEXX010101000) traiga NumRegIdTrib; la app no puede saber si un RFC está inscrito en el SAT."),
        Revisada("CFDI40155"),
        Revisada("CFDI40156"),
        Revisada("CFDI40157"),
        Revisada("CFDI40158"),
        Revisada("CFDI40159"),
        Revisada("CFDI40160"),
        Revisada("CFDI40161"),
        Revisada("CFDI40162"),
        No("CFDI40163", "La columna del complemento requerido viene vacía en todas las claves del catálogo c_ClaveProdServ cargado: la regla no tiene con qué compararse."),
        Parcial("CFDI40164", "Revisa las claves que el catálogo marca con \"Sí\" para IVA o IEPS trasladado cuando el concepto es objeto de impuesto (02); el catálogo cargado no trae impuestos retenidos por clave."),
        Revisada("CFDI40165"),
        Revisada("CFDI40166"),
        Revisada("CFDI40167"),
        Revisada("CFDI40168"),
        Revisada("CFDI40169"),
        Revisada("CFDI40170"),
        Revisada("CFDI40171"),
        Revisada("CFDI40172"),
        Revisada("CFDI40173"),
        Revisada("CFDI40174"),
        Revisada("CFDI40175"),
        Revisada("CFDI40176"),
        Revisada("CFDI40177"),
        Revisada("CFDI40178"),
        Revisada("CFDI40179"),
        Revisada("CFDI40180"),
        Revisada("CFDI40181"),
        Revisada("CFDI40182"),
        Revisada("CFDI40183"),
        Revisada("CFDI40184"),
        Revisada("CFDI40185"),
        Revisada("CFDI40186"),
        No("CFDI40187", "Requiere la lista de contribuyentes obligados (l_LCO) del SAT."),
        Revisada("CFDI40188"),
        Parcial("CFDI40189", "Revisa el nombre cuando el RFC es EXT990101NI1; para los demás RFC haría falta la LRFC."),
        No("CFDI40190", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        Revisada("CFDI40191"),
        No("CFDI40192", "Requiere la lista de RFC inscritos no cancelados (LRFC) del SAT, que no se puede consultar sin conexión."),
        Parcial("CFDI40193", "Revisa el domicilio cuando el RFC es EXT990101NI1 (debe ser el LugarExpedicion); para los demás RFC haría falta la LRFC."),
        No("CFDI40194", "El formato del número de pedimento lo revisa el esquema; las reglas de aduana, patente y año necesitan los catálogos de aduanas y patentes, que la app no carga."),
        Revisada("CFDI40195"),
        Revisada("CFDI40196"),
        Revisada("CFDI40197"),
        Revisada("CFDI40198"),
        No("CFDI40199", "El formato del número de pedimento lo revisa el esquema; las reglas de aduana, patente y año necesitan los catálogos de aduanas y patentes, que la app no carga."),
        Revisada("CFDI40200"),
        Revisada("CFDI40201"),
        Revisada("CFDI40202"),
        Revisada("CFDI40203"),
        Revisada("CFDI40204"),
        Revisada("CFDI40205"),
        Revisada("CFDI40206"),
        Revisada("CFDI40207"),
        Revisada("CFDI40208"),
        Revisada("CFDI40209", "Misma condición que CFDI40206: se informa una sola vez con ese código.", "CFDI40206"),
        Revisada("CFDI40210"),
        Revisada("CFDI40211"),
        Revisada("CFDI40212"),
        Revisada("CFDI40213"),
        Revisada("CFDI40214"),
        Revisada("CFDI40215"),
        Revisada("CFDI40216"),
        Revisada("CFDI40217"),
        Revisada("CFDI40218"),
        Revisada("CFDI40219"),
        Revisada("CFDI40220"),
        Revisada("CFDI40221"),
        Revisada("CFDI40222"),
        Revisada("CFDI140223"),
        No("CFDI140224", "Depende de la marca de retención del emisor en la LRFC del SAT."),
        No("CFDI140225", "Depende de la marca de retención del emisor en la LRFC del SAT."),
        Revisada("CFDI140226"),
        Revisada("CFDI140227"),
        Revisada("CFDI140228"),
        Revisada("CFDI40999", "Se informa cuando una regla falla por un error inesperado del validador."),
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, ReglaSat>> PorCodigo = new(
        () => Todas.ToDictionary(r => r.Codigo, StringComparer.Ordinal));

    public static ReglaSat? Buscar(string codigo) => PorCodigo.Value.GetValueOrDefault(codigo);

    private static ReglaSat Revisada(string codigo, string? nota = null, string? cubiertaPor = null) =>
        new(codigo, EstadoDeRegla.Revisada, nota, cubiertaPor);

    private static ReglaSat Parcial(string codigo, string nota) => new(codigo, EstadoDeRegla.Parcial, nota);

    private static ReglaSat No(string codigo, string motivo) => new(codigo, EstadoDeRegla.NoRevisada, motivo);
}
