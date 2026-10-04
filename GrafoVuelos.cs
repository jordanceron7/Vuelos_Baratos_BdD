// Practica #04 - Implementacion y representacion de arboles y grafos
// Tema: Encuentro de vuelos baratos a partir de una base de datos.
//
// Estructura principal: grafo DIRIGIDO y PONDERADO (lista de adyacencia
// implementada con Dictionary). Los aeropuertos son los vertices y cada vuelo
// es una arista con precio (USD) y duracion (min).
//
// Algoritmos:
//   - Dijkstra con cola de prioridad (PriorityQueue) -> ruta mas barata / mas rapida
//   - BFS (busqueda en anchura, Queue)               -> ruta con menos escalas
//   - Arbol de caminos minimos                       -> arbol que genera Dijkstra

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static VuelosBaratos.Fmt;

namespace VuelosBaratos
{
    /// <summary>Arista dirigida del grafo: un vuelo.</summary>
    public sealed record Vuelo(string Origen, string Destino, string Aerolinea, double Precio, int Duracion);

    /// <summary>Vertice del grafo: un aeropuerto.</summary>
    public sealed record Aeropuerto(string Codigo, string Ciudad, double Lat, double Lon);

    /// <summary>Atributo que se usa como peso de la arista.</summary>
    public enum Criterio { Precio, Duracion }

    /// <summary>Resultado de ejecutar Dijkstra desde un origen.</summary>
    public sealed class ResultadoDijkstra
    {
        public Dictionary<string, double> Dist { get; }
        public Dictionary<string, Vuelo?> Previo { get; }
        public int Inserciones { get; }

        public ResultadoDijkstra(Dictionary<string, double> dist, Dictionary<string, Vuelo?> previo, int inserciones)
        {
            Dist = dist;
            Previo = previo;
            Inserciones = inserciones;
        }
    }

    /// <summary>Utilidades de formato para alinear columnas en consola.</summary>
    internal static class Fmt
    {
        public static string L(object? valor, int ancho) =>
            (Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "").PadRight(ancho);

        public static string R(object? valor, int ancho) =>
            (Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "").PadLeft(ancho);

        public static string F(double valor, int decimales) =>
            valor.ToString("F" + decimales, CultureInfo.InvariantCulture);
    }

    public class GrafoVuelos
    {
        public const double Inf = double.PositiveInfinity;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly Dictionary<string, Aeropuerto> _aeropuertos = new Dictionary<string, Aeropuerto>();
        private readonly Dictionary<string, List<Vuelo>> _adyacencia = new Dictionary<string, List<Vuelo>>();
        private readonly List<string> _orden = new List<string>();   // codigos en orden de insercion

        // ------------------------------------------------------------------
        // Construccion del grafo
        // ------------------------------------------------------------------
        public IReadOnlyList<string> Codigos => _orden;
        public int NumAeropuertos => _orden.Count;
        public int NumVuelos => _adyacencia.Values.Sum(lista => lista.Count);

        public bool Existe(string codigo) => _aeropuertos.ContainsKey(codigo);
        public Aeropuerto Obtener(string codigo) => _aeropuertos[codigo];
        public IReadOnlyList<Vuelo> VuelosDesde(string codigo) => _adyacencia[codigo];

        public void AgregarAeropuerto(string codigo, string ciudad, double lat = 0.0, double lon = 0.0)
        {
            codigo = codigo.Trim().ToUpperInvariant();
            if (!_aeropuertos.ContainsKey(codigo))
            {
                _aeropuertos[codigo] = new Aeropuerto(codigo, ciudad, lat, lon);
                _adyacencia[codigo] = new List<Vuelo>();
                _orden.Add(codigo);
            }
        }

        public void AgregarVuelo(string origen, string destino, string aerolinea, double precio, int duracion)
        {
            origen = origen.Trim().ToUpperInvariant();
            destino = destino.Trim().ToUpperInvariant();
            if (!_aeropuertos.ContainsKey(origen) || !_aeropuertos.ContainsKey(destino))
                throw new ArgumentException("Aeropuerto inexistente en el vuelo " + origen + "->" + destino);
            if (origen == destino)
                throw new ArgumentException("El origen y el destino no pueden ser iguales");
            if (precio <= 0 || duracion <= 0)
                throw new ArgumentException("Precio y duracion deben ser positivos");
            _adyacencia[origen].Add(new Vuelo(origen, destino, aerolinea, precio, duracion));
        }

        /// <summary>Lee la base de datos ficticia desde un archivo de texto.</summary>
        public void CargarArchivo(string ruta)
        {
            var lineas = File.ReadAllLines(ruta)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && l[0] != '#')
                .ToList();

            foreach (var linea in lineas)            // primero los vertices
            {
                var p = linea.Split(';');
                if (p[0] != "A") continue;
                try
                {
                    AgregarAeropuerto(p[1], p[2], double.Parse(p[3], Inv), double.Parse(p[4], Inv));
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException)
                {
                    Console.WriteLine("  [aviso] linea ignorada (" + linea + "): " + e.Message);
                }
            }

            foreach (var linea in lineas)            // luego las aristas
            {
                var p = linea.Split(';');
                if (p[0] != "V") continue;
                try
                {
                    AgregarVuelo(p[1], p[2], p[3], double.Parse(p[4], Inv), int.Parse(p[5], Inv));
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException || e is IndexOutOfRangeException)
                {
                    Console.WriteLine("  [aviso] linea ignorada (" + linea + "): " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------
        // Consultas basicas
        // ------------------------------------------------------------------
        public List<Vuelo> VuelosDirectos(string origen, string destino) =>
            _adyacencia[origen].Where(v => v.Destino == destino).ToList();

        public Vuelo? VueloMasBarato(string origen, string destino) =>
            VuelosDirectos(origen, destino).OrderBy(v => v.Precio).FirstOrDefault();

        private IEnumerable<string> Ordenados() => _orden.OrderBy(c => c, StringComparer.Ordinal);

        private int GradoSalida(string codigo) => _adyacencia[codigo].Select(v => v.Destino).Distinct().Count();

        // ------------------------------------------------------------------
        // Algoritmos
        // ------------------------------------------------------------------

        /// <summary>Orden de la cola de prioridad: primero el menor costo; si hay empate, el codigo
        /// (la PriorityQueue de .NET no garantiza orden FIFO entre prioridades iguales).</summary>
        private sealed class ComparadorPrioridad : IComparer<(double Costo, string Codigo)>
        {
            public int Compare((double Costo, string Codigo) a, (double Costo, string Codigo) b)
            {
                int c = a.Costo.CompareTo(b.Costo);
                return c != 0 ? c : string.CompareOrdinal(a.Codigo, b.Codigo);
            }
        }

        /// <summary>Caminos minimos desde 'origen'. Previo[x] es el Vuelo usado para llegar a x.</summary>
        public ResultadoDijkstra Dijkstra(string origen, Criterio criterio = Criterio.Precio)
        {
            var dist = new Dictionary<string, double>();
            var previo = new Dictionary<string, Vuelo?>();
            foreach (var c in _orden)
            {
                dist[c] = Inf;
                previo[c] = null;
            }
            dist[origen] = 0;

            var cola = new PriorityQueue<string, (double Costo, string Codigo)>(new ComparadorPrioridad());
            cola.Enqueue(origen, (0.0, origen));
            int inserciones = 1;
            var visitados = new HashSet<string>();

            while (cola.TryDequeue(out string? u, out (double Costo, string Codigo) prioridad))
            {
                if (visitados.Contains(u)) continue;      // entrada obsoleta
                visitados.Add(u);
                double d = prioridad.Costo;
                foreach (var vuelo in _adyacencia[u])
                {
                    double peso = criterio == Criterio.Precio ? vuelo.Precio : vuelo.Duracion;
                    double nd = d + peso;
                    if (nd < dist[vuelo.Destino])         // relajacion de la arista
                    {
                        dist[vuelo.Destino] = nd;
                        previo[vuelo.Destino] = vuelo;
                        cola.Enqueue(vuelo.Destino, (nd, vuelo.Destino));
                        inserciones++;
                    }
                }
            }
            return new ResultadoDijkstra(dist, previo, inserciones);
        }

        /// <summary>Reconstruye la ruta retrocediendo por Previo. Devuelve null si no hay camino.</summary>
        public static List<Vuelo>? Reconstruir(Dictionary<string, Vuelo?> previo, string origen, string destino)
        {
            var tramos = new List<Vuelo>();
            string actual = destino;
            while (actual != origen)
            {
                if (!previo.TryGetValue(actual, out Vuelo? vuelo) || vuelo == null) return null;
                tramos.Add(vuelo);
                actual = vuelo.Origen;
            }
            tramos.Reverse();
            return tramos;
        }

        public (List<Vuelo>? Tramos, double Costo) RutaOptima(string origen, string destino, Criterio criterio)
        {
            var r = Dijkstra(origen, criterio);
            if (double.IsPositiveInfinity(r.Dist[destino])) return (null, Inf);
            return (Reconstruir(r.Previo, origen, destino), r.Dist[destino]);
        }

        /// <summary>BFS: ruta con menor numero de vuelos; en cada tramo se elige el vuelo mas barato.</summary>
        public List<Vuelo>? MenosEscalas(string origen, string destino) =>
            MenosEscalas(origen, destino, out _);

        public List<Vuelo>? MenosEscalas(string origen, string destino, out int nodosExtraidos)
        {
            var previo = new Dictionary<string, string?> { [origen] = null };
            var cola = new Queue<string>();
            cola.Enqueue(origen);
            nodosExtraidos = 0;
            while (cola.Count > 0)
            {
                string u = cola.Dequeue();
                nodosExtraidos++;
                if (u == destino) break;
                foreach (var vuelo in _adyacencia[u])
                {
                    if (!previo.ContainsKey(vuelo.Destino))
                    {
                        previo[vuelo.Destino] = u;
                        cola.Enqueue(vuelo.Destino);
                    }
                }
            }
            if (!previo.ContainsKey(destino)) return null;

            var camino = new List<string> { destino };
            string actual = destino;
            while (previo[actual] != null)
            {
                actual = previo[actual]!;
                camino.Add(actual);
            }
            camino.Reverse();

            var tramos = new List<Vuelo>();
            for (int i = 0; i + 1 < camino.Count; i++)
                tramos.Add(VueloMasBarato(camino[i], camino[i + 1])!);
            return tramos;
        }

        // ------------------------------------------------------------------
        // Reporteria
        // ------------------------------------------------------------------
        public void ReporteAeropuertos()
        {
            Console.WriteLine();
            Console.WriteLine(L("COD", 5) + L("CIUDAD", 28) + R("LAT", 9) + R("LON", 10) + R("SALIDAS", 9));
            Console.WriteLine(new string('-', 61));
            foreach (var c in Ordenados())
            {
                var a = _aeropuertos[c];
                Console.WriteLine(L(c, 5) + L(a.Ciudad, 28) + R(F(a.Lat, 4), 9) + R(F(a.Lon, 4), 10)
                                  + R(_adyacencia[c].Count, 9));
            }
        }

        public void ReporteAdyacencia()
        {
            Console.WriteLine();
            Console.WriteLine("LISTA DE ADYACENCIA (origen -> destino [aerolinea, $precio, min])");
            Console.WriteLine(new string('-', 72));
            foreach (var c in Ordenados())
            {
                if (_adyacencia[c].Count == 0)
                {
                    Console.WriteLine(c + " -> (sin vuelos de salida)");
                    continue;
                }
                Console.WriteLine(c + " ->");
                var ordenados = _adyacencia[c]
                    .OrderBy(v => v.Destino, StringComparer.Ordinal)
                    .ThenBy(v => v.Precio);
                foreach (var v in ordenados)
                    Console.WriteLine("      " + v.Destino + "  [" + v.Aerolinea + ", $" + F(v.Precio, 0)
                                      + ", " + v.Duracion + " min]");
            }
        }

        public void MatrizPrecios()
        {
            var cods = Ordenados().ToList();
            Console.WriteLine();
            Console.WriteLine("MATRIZ DE ADYACENCIA (precio minimo del vuelo directo, USD)");
            Console.WriteLine("     " + string.Concat(cods.Select(c => R(c, 5))));
            foreach (var o in cods)
            {
                string fila = L(o, 5);
                foreach (var d in cods)
                {
                    var v = VueloMasBarato(o, d);
                    fila += v != null ? R(F(v.Precio, 0), 5) : R(".", 5);
                }
                Console.WriteLine(fila);
            }
        }

        public void Estadisticas()
        {
            int n = NumAeropuertos;
            int rutas = _orden.Sum(c => GradoSalida(c));
            var gradoEnt = _orden.ToDictionary(c => c, c => 0);
            foreach (var c in _orden)
                foreach (var d in _adyacencia[c].Select(v => v.Destino).Distinct())
                    gradoEnt[d]++;

            Console.WriteLine();
            Console.WriteLine("ESTADISTICAS DEL GRAFO");
            Console.WriteLine(new string('-', 40));
            Console.WriteLine("Vertices (aeropuertos)       : " + n);
            Console.WriteLine("Aristas (vuelos)             : " + NumVuelos);
            Console.WriteLine("Rutas distintas (pares O-D)  : " + rutas);
            Console.WriteLine("Densidad |E|/(|V|(|V|-1))    : " + F(rutas / (double)(n * (n - 1)), 3));
            Console.WriteLine();
            Console.WriteLine(L("COD", 6) + R("GRADO SALIDA", 14) + R("GRADO ENTRADA", 16));
            foreach (var c in _orden.OrderByDescending(x => GradoSalida(x)))
                Console.WriteLine(L(c, 6) + R(GradoSalida(c), 14) + R(gradoEnt[c], 16));
        }

        public void ImprimirRuta(List<Vuelo>? tramos, string titulo)
        {
            Console.WriteLine();
            Console.WriteLine(titulo);
            Console.WriteLine(new string('-', 60));
            if (tramos == null || tramos.Count == 0)
            {
                Console.WriteLine("No existe una ruta entre los aeropuertos indicados.");
                return;
            }
            double totalP = 0;
            int totalT = 0;
            for (int i = 0; i < tramos.Count; i++)
            {
                var v = tramos[i];
                Console.WriteLine("  " + (i + 1) + ". " + v.Origen + " -> " + v.Destino + "  " + L(v.Aerolinea, 14)
                                  + " $" + R(F(v.Precio, 0), 6) + "  " + R(v.Duracion, 4) + " min");
                totalP += v.Precio;
                totalT += v.Duracion;
            }
            Console.WriteLine("  Escalas: " + (tramos.Count - 1) + " | Precio total: $" + F(totalP, 0)
                              + " | Duracion en vuelo: " + totalT + " min (" + (totalT / 60) + " h " + (totalT % 60) + " min)");
        }

        /// <summary>Muestra el arbol de caminos minimos (raiz = origen).</summary>
        public void ArbolCaminosMinimos(string origen, Criterio criterio = Criterio.Precio)
        {
            var r = Dijkstra(origen, criterio);
            var hijos = _orden.ToDictionary(c => c, c => new List<string>());
            foreach (var dest in _orden)
            {
                var v = r.Previo[dest];
                if (v != null) hijos[v.Origen].Add(dest);
            }

            string unidad = criterio == Criterio.Precio ? "$" : "";
            string sufijo = criterio == Criterio.Precio ? "" : " min";
            Console.WriteLine();
            Console.WriteLine("ARBOL DE CAMINOS MINIMOS desde " + origen + " (criterio: " + criterio.ToString().ToLowerInvariant() + ")");
            Console.WriteLine(new string('-', 60));

            MostrarNodo(origen, "", true, true, hijos, r.Dist, unidad, sufijo);

            var sin = _orden.Where(c => double.IsPositiveInfinity(r.Dist[c])).ToList();
            if (sin.Count > 0) Console.WriteLine("Inalcanzables: " + string.Join(", ", sin));
        }

        private void MostrarNodo(string nodo, string prefijo, bool ultimo, bool raiz,
                                 Dictionary<string, List<string>> hijos, Dictionary<string, double> dist,
                                 string unidad, string sufijo)
        {
            string etiqueta = nodo + " (" + _aeropuertos[nodo].Ciudad + ")";
            if (raiz)
            {
                Console.WriteLine(etiqueta + "  [acum: " + unidad + "0" + sufijo + "]");
            }
            else
            {
                string rama = ultimo ? "└── " : "├── ";
                Console.WriteLine(prefijo + rama + etiqueta + "  [acum: " + unidad + F(dist[nodo], 0) + sufijo + "]");
            }

            var hs = hijos[nodo].OrderBy(x => dist[x]).ToList();
            for (int i = 0; i < hs.Count; i++)
            {
                string ext = raiz ? "" : (ultimo ? "    " : "│   ");
                MostrarNodo(hs[i], prefijo + ext, i == hs.Count - 1, false, hijos, dist, unidad, sufijo);
            }
        }
    }
}
