// Practica #04 - Arboles y Grafos | Buscador de vuelos baratos (consola)
// Uso:  dotnet run -c Release            -> menu interactivo
//       dotnet run -c Release -- --demo  -> ejecuta las consultas principales

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using static VuelosBaratos.Fmt;

namespace VuelosBaratos
{
    /// <summary>Generador pseudoaleatorio simple y reproducible (LCG de 64 bits).</summary>
    internal sealed class Generador
    {
        private ulong _estado;

        public Generador(ulong semilla)
        {
            _estado = semilla;
        }

        /// <summary>Entero en el intervalo [0, max).</summary>
        public int Next(int max)
        {
            unchecked
            {
                _estado = _estado * 6364136223846793005UL + 1442695040888963407UL;
            }
            return (int)((_estado >> 33) % (ulong)max);
        }
    }

    internal static class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static void Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
            CultureInfo.CurrentCulture = Inv;
            CultureInfo.DefaultThreadCurrentCulture = Inv;

            string? ruta = BuscarArchivo("vuelos.txt");
            if (ruta == null)
            {
                Console.WriteLine("No se encontro el archivo vuelos.txt (debe estar junto al programa).");
                return;
            }

            var grafo = new GrafoVuelos();
            grafo.CargarArchivo(ruta);

            if (args.Contains("--demo")) Demo(grafo);
            else Menu(grafo);
        }

        private static string? BuscarArchivo(string nombre)
        {
            string[] candidatos =
            {
                Path.Combine(Directory.GetCurrentDirectory(), nombre),
                Path.Combine(AppContext.BaseDirectory, nombre)
            };
            return candidatos.FirstOrDefault(File.Exists);
        }

        // ------------------------------------------------------------------
        // Entrada de datos
        // ------------------------------------------------------------------
        private static string Leer(string mensaje)
        {
            Console.Write(mensaje);
            string? linea = Console.ReadLine();
            if (linea == null) throw new EndOfStreamException();
            return linea.Trim();
        }

        private static string PedirCodigo(GrafoVuelos g, string mensaje)
        {
            while (true)
            {
                string c = Leer(mensaje).ToUpperInvariant();
                if (g.Existe(c)) return c;
                Console.WriteLine("  Codigo no valido. Opciones: " + string.Join(", ", g.Codigos.OrderBy(x => x, StringComparer.Ordinal)));
            }
        }

        // ------------------------------------------------------------------
        // Menu
        // ------------------------------------------------------------------
        private const string TextoMenu = @"
=========== BUSCADOR DE VUELOS BARATOS (grafo) ===========
 1. Listar aeropuertos (vertices)
 2. Lista de adyacencia (todos los vuelos)
 3. Matriz de adyacencia de precios
 4. Estadisticas del grafo
 5. Ruta mas BARATA (Dijkstra por precio)
 6. Ruta mas RAPIDA (Dijkstra por duracion)
 7. Ruta con MENOS ESCALAS (BFS)
 8. Arbol de caminos minimos desde un origen
 9. Agregar un vuelo nuevo
10. Analisis de tiempo de ejecucion
11. Exportar grafico (SVG)
 0. Salir
===========================================================";

        private static void Menu(GrafoVuelos g)
        {
            while (true)
            {
                Console.WriteLine(TextoMenu);
                try
                {
                    string op = Leer("Seleccione una opcion: ");
                    switch (op)
                    {
                        case "1": g.ReporteAeropuertos(); break;
                        case "2": g.ReporteAdyacencia(); break;
                        case "3": g.MatrizPrecios(); break;
                        case "4": g.Estadisticas(); break;
                        case "5":
                        case "6":
                        case "7":
                            ConsultarRuta(g, op);
                            break;
                        case "8":
                            {
                                string o = PedirCodigo(g, "Aeropuerto de origen: ");
                                string crit = Leer("Criterio (precio/duracion) [precio]: ").ToLowerInvariant();
                                g.ArbolCaminosMinimos(o, crit == "duracion" ? Criterio.Duracion : Criterio.Precio);
                                break;
                            }
                        case "9": AgregarVuelo(g); break;
                        case "10": Benchmark(); break;
                        case "11": ExportarSvg(g); break;
                        case "0":
                            Console.WriteLine("Hasta luego.");
                            return;
                        default:
                            Console.WriteLine("Opcion no valida.");
                            break;
                    }
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
                {
                    Console.WriteLine("Dato invalido: " + e.Message);
                }
            }
        }

        private static void ConsultarRuta(GrafoVuelos g, string op)
        {
            string o = PedirCodigo(g, "Aeropuerto de origen : ");
            string d = PedirCodigo(g, "Aeropuerto de destino: ");
            if (op == "7")
            {
                g.ImprimirRuta(g.MenosEscalas(o, d), "RUTA CON MENOS ESCALAS " + o + " -> " + d);
                return;
            }
            Criterio crit = op == "5" ? Criterio.Precio : Criterio.Duracion;
            var sw = Stopwatch.StartNew();
            var (tramos, _) = g.RutaOptima(o, d, crit);
            sw.Stop();
            g.ImprimirRuta(tramos, "RUTA MAS " + (op == "5" ? "BARATA " : "RAPIDA ") + o + " -> " + d);
            Console.WriteLine("  Tiempo de busqueda: " + F(sw.Elapsed.TotalMilliseconds, 3) + " ms");
        }

        private static void AgregarVuelo(GrafoVuelos g)
        {
            string o = PedirCodigo(g, "Origen : ");
            string d = PedirCodigo(g, "Destino: ");
            string aerolinea = Leer("Aerolinea: ");
            if (aerolinea.Length == 0) aerolinea = "Nueva";
            double precio = double.Parse(Leer("Precio (USD): ").Replace(',', '.'), Inv);
            int duracion = int.Parse(Leer("Duracion (min): "), Inv);
            g.AgregarVuelo(o, d, aerolinea, precio, duracion);
            Console.WriteLine("Vuelo agregado correctamente.");
        }

        // ------------------------------------------------------------------
        // Demo automatica (sirve para capturas de pantalla)
        // ------------------------------------------------------------------
        private static void Demo(GrafoVuelos g)
        {
            g.ReporteAeropuertos();
            g.ReporteAdyacencia();
            g.MatrizPrecios();
            g.Estadisticas();
            var consultas = new[] { ("UIO", "SCY"), ("LOH", "BOG"), ("TUA", "LIM") };
            foreach (var (o, d) in consultas)
            {
                g.ImprimirRuta(g.RutaOptima(o, d, Criterio.Precio).Tramos, "RUTA MAS BARATA " + o + " -> " + d);
                g.ImprimirRuta(g.RutaOptima(o, d, Criterio.Duracion).Tramos, "RUTA MAS RAPIDA " + o + " -> " + d);
                g.ImprimirRuta(g.MenosEscalas(o, d), "RUTA CON MENOS ESCALAS " + o + " -> " + d);
            }
            g.ArbolCaminosMinimos("UIO", Criterio.Precio);
            Benchmark();
        }

        // ------------------------------------------------------------------
        // Medicion de tiempos
        // ------------------------------------------------------------------
        private static GrafoVuelos GrafoAleatorio(int n, int factor = 5, ulong semilla = 7)
        {
            var rnd = new Generador(semilla);
            var g = new GrafoVuelos();
            for (int i = 0; i < n; i++) g.AgregarAeropuerto("N" + i, "Ciudad " + i);

            // Anillo N0->N1->...->N0: garantiza que todos los vertices sean alcanzables
            for (int i = 0; i < n; i++)
            {
                int precio = 30 + rnd.Next(371);
                int duracion = 30 + rnd.Next(271);
                g.AgregarVuelo("N" + i, "N" + ((i + 1) % n), "Anillo", precio, duracion);
            }
            // Aristas aleatorias hasta completar |E| = factor * |V|
            for (int k = 0; k < n * (factor - 1); k++)
            {
                int a = rnd.Next(n);
                int b = rnd.Next(n - 1);
                if (b >= a) b++;
                int precio = 30 + rnd.Next(371);
                int duracion = 30 + rnd.Next(271);
                g.AgregarVuelo("N" + a, "N" + b, "Aleatoria", precio, duracion);
            }
            return g;
        }

        private static void Benchmark(int[]? tamanos = null, int repeticiones = 5)
        {
            tamanos ??= new[] { 100, 1000, 10000, 50000 };

            // Calentamiento: evita que la compilacion JIT infle la primera medicion
            var calentamiento = GrafoAleatorio(200);
            calentamiento.Dijkstra("N0", Criterio.Precio);
            calentamiento.MenosEscalas("N0", "N199");

            Console.WriteLine();
            Console.WriteLine("ANALISIS DE TIEMPO DE EJECUCION (grafos aleatorios, |E| = 5|V|)");
            Console.WriteLine(R("|V|", 8) + R("|E|", 10) + R("Construir (ms)", 16) + R("Dijkstra (ms)", 16)
                              + R("Inserciones", 13) + R("BFS (ms)", 12) + R("BFS nodos", 11));
            Console.WriteLine(new string('-', 86));

            foreach (int n in tamanos)
            {
                var sw = Stopwatch.StartNew();
                var g = GrafoAleatorio(n);
                double tConstruir = sw.Elapsed.TotalMilliseconds;

                double tDij = 0, tBfs = 0;
                int inserciones = 0, nodos = 0;
                for (int r = 0; r < repeticiones; r++)
                {
                    sw.Restart();
                    var res = g.Dijkstra("N0", Criterio.Precio);
                    tDij += sw.Elapsed.TotalMilliseconds;
                    inserciones = res.Inserciones;

                    sw.Restart();
                    g.MenosEscalas("N0", "N" + (n - 1), out nodos);
                    tBfs += sw.Elapsed.TotalMilliseconds;
                }

                Console.WriteLine(R(n, 8) + R(g.NumVuelos, 10) + R(F(tConstruir, 2), 16)
                                  + R(F(tDij / repeticiones, 2), 16) + R(inserciones, 13)
                                  + R(F(tBfs / repeticiones, 2), 12) + R(nodos, 11));
            }
        }

        // ------------------------------------------------------------------
        // Exportacion del grafo a SVG (se abre en cualquier navegador)
        // ------------------------------------------------------------------
        private static string N1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

        private static void ExportarSvg(GrafoVuelos g, string archivo = "red_vuelos.svg")
        {
            const int ancho = 1000, alto = 640, margen = 70;
            const double radio = 15.0;

            var cods = g.Codigos;
            double minLon = cods.Min(c => g.Obtener(c).Lon), maxLon = cods.Max(c => g.Obtener(c).Lon);
            double minLat = cods.Min(c => g.Obtener(c).Lat), maxLat = cods.Max(c => g.Obtener(c).Lat);
            double X(double lon) => margen + (lon - minLon) / (maxLon - minLon) * (ancho - 2 * margen);
            double Y(double lat) => alto - margen - (lat - minLat) / (maxLat - minLat) * (alto - 2 * margen);

            var sb = new StringBuilder();
            sb.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" + ancho + "\" height=\"" + alto
                          + "\" viewBox=\"0 0 " + ancho + " " + alto + "\">");
            sb.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");
            sb.AppendLine("<defs><marker id=\"flecha\" markerWidth=\"8\" markerHeight=\"8\" refX=\"7\" refY=\"4\" orient=\"auto\">"
                          + "<path d=\"M0,0 L8,4 L0,8 z\" fill=\"#7a8aa0\"/></marker></defs>");
            sb.AppendLine("<text x=\"" + (ancho / 2) + "\" y=\"32\" text-anchor=\"middle\" font-family=\"Arial\" font-size=\"16\">"
                          + "Red de vuelos (grafo dirigido) - base de datos ficticia</text>");

            // Aristas: una flecha por cada par origen-destino (curvas para separar los dos sentidos)
            foreach (var o in cods)
            {
                var po = g.Obtener(o);
                foreach (var d in g.VuelosDesde(o).Select(v => v.Destino).Distinct())
                {
                    var pd = g.Obtener(d);
                    double x1 = X(po.Lon), y1 = Y(po.Lat), x2 = X(pd.Lon), y2 = Y(pd.Lat);
                    double dx = x2 - x1, dy = y2 - y1;
                    double cx = (x1 + x2) / 2 - dy * 0.12, cy = (y1 + y2) / 2 + dx * 0.12;

                    double ux = cx - x1, uy = cy - y1, lu = Math.Sqrt(ux * ux + uy * uy);
                    double vx = x2 - cx, vy = y2 - cy, lv = Math.Sqrt(vx * vx + vy * vy);
                    double sx = x1 + ux / lu * radio, sy = y1 + uy / lu * radio;
                    double ex = x2 - vx / lv * (radio + 2), ey = y2 - vy / lv * (radio + 2);

                    sb.AppendLine("<path d=\"M" + N1(sx) + " " + N1(sy) + " Q" + N1(cx) + " " + N1(cy) + " " + N1(ex) + " " + N1(ey)
                                  + "\" fill=\"none\" stroke=\"#7a8aa0\" stroke-width=\"1.2\" marker-end=\"url(#flecha)\"/>");
                }
            }

            // Vertices
            foreach (var c in cods)
            {
                var a = g.Obtener(c);
                sb.AppendLine("<circle cx=\"" + N1(X(a.Lon)) + "\" cy=\"" + N1(Y(a.Lat)) + "\" r=\"15\" fill=\"#1f4e79\"/>");
                sb.AppendLine("<text x=\"" + N1(X(a.Lon)) + "\" y=\"" + N1(Y(a.Lat)) + "\" dy=\"3.5\" text-anchor=\"middle\" "
                              + "font-family=\"Arial\" font-size=\"10\" font-weight=\"bold\" fill=\"white\">" + c + "</text>");
            }
            sb.AppendLine("</svg>");

            File.WriteAllText(archivo, sb.ToString(), new UTF8Encoding(false));
            Console.WriteLine("Grafico guardado en " + Path.GetFullPath(archivo));
        }
    }
}
