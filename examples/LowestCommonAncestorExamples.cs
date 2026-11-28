using System;
using System.Collections.Generic;

// Lowest Common Ancestor with binary lifting on this tree:
//         0
//        / \
//       1   2
//      / \ / \
//     3  4 5  6
//
// Hand-traced before writing:
//   Depths: depth(0)=0, depth(1)=depth(2)=1, depth(3..6)=2.
//   Parents: parent(0)=0, parent(1)=0, parent(2)=0, parent(3)=1,
//            parent(4)=1, parent(5)=2, parent(6)=2.
//   up[1][3] = up[0][up[0][3]] = up[0][1] = 0.
//   LCA(3,4)=1, LCA(3,5)=0, LCA(5,6)=2, LCA(3,6)=0.
//   Distance(u,v) = depth[u] + depth[v] - 2 * depth[LCA(u,v)].
//   Distance(3,6) = 2+2-0 = 4, Distance(3,4) = 2+2-2 = 2,
//   Distance(4,5) = 2+2-0 = 4.
const int nodeCount = 7;
const int log = 3; // 2^3 = 8 covers every jump for 7 nodes.

var children = new List<int>[nodeCount];
for (int i = 0; i < nodeCount; i++)
{
    children[i] = new List<int>();
}
children[0].Add(1);
children[0].Add(2);
children[1].Add(3);
children[1].Add(4);
children[2].Add(5);
children[2].Add(6);

// up[k][v] = the 2^k-th ancestor of v. up[0][v] is the parent.
var up = new int[log][];
for (int k = 0; k < log; k++)
{
    up[k] = new int[nodeCount];
}
var depth = new int[nodeCount];

// DFS from the root fills depth[] and up[0][] (parents).
var stack = new Stack<int>();
stack.Push(0);
up[0][0] = 0; // The root is its own parent.
depth[0] = 0;
while (stack.Count > 0)
{
    int u = stack.Pop();
    foreach (int v in children[u])
    {
        depth[v] = depth[u] + 1;
        up[0][v] = u;
        stack.Push(v);
    }
}

// Fill the higher jump levels: up[k][v] = up[k-1][ up[k-1][v] ].
for (int k = 1; k < log; k++)
{
    for (int v = 0; v < nodeCount; v++)
    {
        up[k][v] = up[k - 1][up[k - 1][v]];
    }
}

Console.WriteLine($"LCA(3,4)={Lca(3, 4)}"); // Expected: LCA(3,4)=1
Console.WriteLine($"LCA(3,5)={Lca(3, 5)}"); // Expected: LCA(3,5)=0
Console.WriteLine($"LCA(5,6)={Lca(5, 6)}"); // Expected: LCA(5,6)=2
Console.WriteLine($"LCA(3,6)={Lca(3, 6)}"); // Expected: LCA(3,6)=0
Console.WriteLine($"Distance(3,6)={Distance(3, 6)}"); // Expected: 4
Console.WriteLine($"Distance(3,4)={Distance(3, 4)}"); // Expected: 2
Console.WriteLine($"Distance(4,5)={Distance(4, 5)}"); // Expected: 4

int Lca(int u, int v)
{
    if (depth[u] < depth[v])
    {
        (u, v) = (v, u); // u is now the deeper node.
    }

    // Lift u up to the depth of v, using the bits of the difference.
    int diff = depth[u] - depth[v];
    for (int k = 0; k < log; k++)
    {
        if ((diff & (1 << k)) != 0)
        {
            u = up[k][u];
        }
    }

    if (u == v)
    {
        return u;
    }

    // Lift both together, largest jump first, stopping just below the LCA.
    for (int k = log - 1; k >= 0; k--)
    {
        if (up[k][u] != up[k][v])
        {
            u = up[k][u];
            v = up[k][v];
        }
    }

    return up[0][u];
}

int Distance(int u, int v)
{
    int ancestor = Lca(u, v);
    return depth[u] + depth[v] - 2 * depth[ancestor];
}
