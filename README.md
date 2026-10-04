# Práctica #04 – Árboles y grafos: buscador de vuelos baratos (C#)

Universidad Estatal Amazónica – Estructura de Datos 

Grafo dirigido y ponderado (lista de adyacencia con `Dictionary<string, List<Vuelo>>`) que modela
una base de datos **ficticia** de 12 aeropuertos y 38 vuelos leída desde `vuelos.txt`.

## Qué hace
- Reportería: aeropuertos, lista de adyacencia, matriz de precios y estadísticas del grafo.
- Ruta más barata y más rápida (Dijkstra con `PriorityQueue`).
- Ruta con menos escalas (BFS con `Queue`).
- Árbol de caminos mínimos desde un origen.
- Análisis de tiempo de ejecución (`Stopwatch`) sobre grafos aleatorios reproducibles.
- Exportación del grafo a `red_vuelos.svg` (se abre en el navegador).

## Cómo ejecutarlo
Requiere el SDK de .NET 6 o superior (compruebe con `dotnet --version`).
Si su versión no es 8, cambie `net8.0` por `net6.0`, `net7.0`, `net9.0`, etc. en `VuelosBaratos.csproj`.

```bash
cd VuelosBaratos
dotnet run -c Release             # menú interactivo
dotnet run -c Release -- --demo   # ejecuta las consultas principales automáticamente
```

Si usa Visual Studio: cree un proyecto "Aplicación de consola" y agregue `GrafoVuelos.cs` y
`Program.cs`; en las propiedades de `vuelos.txt` marque "Copiar en el directorio de salida: Copiar si es posterior".
Ejecute en configuración **Release** para medir tiempos.

## Formato de `vuelos.txt`
```
A;codigo;ciudad;latitud;longitud
V;origen;destino;aerolinea;precio_usd;duracion_min
```
