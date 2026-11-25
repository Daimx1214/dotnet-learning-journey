# Eulerian Path and Circuit

An Eulerian path is a trail that uses every edge of a graph exactly once. An Eulerian circuit is an Eulerian path that starts and ends at the same vertex. The question of whether one exists is answered entirely by vertex degrees, which is what makes this problem elegant.

## The Degree Conditions

For an undirected graph, ignoring isolated vertices:

- **Eulerian circuit** exists if and only if the graph is connected (ignoring isolated vertices) and **every** vertex has even degree.
- **Eulerian path** (but not a circuit) exists if and only if the graph is connected and **exactly two** vertices have odd degree. The path must start at one odd vertex and end at the other.

The intuition: every time a trail passes through a vertex it uses two edges, one to enter and one to leave. A vertex in the middle of a trail therefore has even degree. Only the start and end vertices can have odd degree, because the trail leaves the start once more than it enters, and enters the end once more than it leaves.

For a directed graph the condition is about in-degree and out-degree: a circuit needs in-degree equal to out-degree at every vertex, and a path needs at most one vertex with out-degree minus in-degree equal to 1 (the start) and one with in-degree minus out-degree equal to 1 (the end).

## Hierholzer's Algorithm

Once the degree condition holds, Hierholzer's algorithm finds the trail in O(V + E):

```text
Hierholzer(graph, start):
    stack = [start]
    circuit = []
    while stack is not empty:
        v = stack.top()
        if v has an unused edge (v, u):
            mark (v, u) used
            stack.push(u)
        else:
            circuit.append(stack.pop())
    return reverse(circuit)
```

The algorithm walks along unused edges, pushing vertices onto a stack. When it reaches a dead end, it pops vertices into the circuit. Because every vertex has even degree (except the two odd ones), a dead end can only happen at the start vertex, so the walk always finds a way to splice detours in. Each edge is used exactly once.

## C# Sketch

```csharp
List<int> EulerianPath(List<int>[] graph, int start)
{
    var edgeCount = new Dictionary<(int, int), int>();
    // Build an adjacency list with edge multiplicities for multigraphs.
    var stack = new Stack<int>();
    var path = new List<int>();
    stack.Push(start);

    while (stack.Count > 0)
    {
        int v = stack.Peek();
        if (graph[v].Count > 0)
        {
            int u = graph[v][^1];
            graph[v].RemoveAt(graph[v].Count - 1);
            graph[u].Remove(v); // remove the reverse edge in an undirected graph
            stack.Push(u);
        }
        else
        {
            path.Add(stack.Pop());
        }
    }

    path.Reverse();
    return path;
}
```

## Worked Example

Graph with edges A-B, B-C, C-D, D-A, A-C. Written as a cycle with one diagonal, this is the graph A-B-C-D-A-C.

Degrees, counting each edge once per endpoint:

| Vertex | Edges | Degree |
|--------|-------|--------|
| A | A-B, D-A, A-C | 3 (odd) |
| B | A-B, B-C | 2 (even) |
| C | B-C, C-D, A-C | 3 (odd) |
| D | C-D, D-A | 2 (even) |

Exactly two vertices have odd degree, A and C, so an Eulerian path exists but no circuit. The path must start at A and end at C (or the reverse).

The path **A-B-C-D-A-C** uses every edge exactly once:

1. A -> B uses edge A-B.
2. B -> C uses edge B-C.
3. C -> D uses edge C-D.
4. D -> A uses edge D-A.
5. A -> C uses edge A-C.

All 5 edges are used, each once, starting at A and ending at C. That is the Eulerian path.

A simpler check: the graph A-B-C-D-A (without the diagonal) has every degree 2, so it has an Eulerian circuit, for example A-B-C-D-A. Adding the diagonal A-C makes A and C odd, turning the circuit into a path.

## When to Use It

- Route inspection: a postman or snowplow route that covers every street once.
- DNA fragment assembly: de Bruijn graphs use Eulerian paths to reconstruct sequences.
- Checking whether a graph can be drawn in one stroke without lifting the pen, the classic "house" puzzle.

## Key Takeaways

- An Eulerian circuit needs a connected graph where every vertex has even degree.
- An Eulerian path needs exactly two odd-degree vertices, and it starts at one and ends at the other.
- In the worked example A-B-C-D-A-C, degrees are A=3, B=2, C=3, D=2, so the path A-B-C-D-A-C uses all 5 edges once.
- Hierholzer's algorithm finds the trail in O(V + E) by walking unused edges and backtracking into the circuit.
- A trail enters and leaves each middle vertex in pairs, which is why middle vertices always have even degree.
