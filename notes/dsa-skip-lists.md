# Skip Lists

A skip list is a probabilistic data structure that gives a linked list the
speed of a balanced tree. Each element lives in a tower of nodes spanning
several levels. The bottom level holds every element in sorted order, and
each higher level holds a sparser subset that acts as an express lane,
letting a search skip over large stretches of the list.

## Structure

Picture a sorted linked list, then add a second level above it that links
only every second element, a third level linking every fourth, and so on.
A search starts at the top level, moves right while the next key is smaller
than the target, drops down a level when it is not, and repeats until it
reaches the bottom level. Each level halves the distance that remains, which
is why the search is fast without any rebalancing.

```
Level 3:  3 ---------------------------- 21
Level 2:  3 -------- 9 -------- 17 ------ 21
Level 1:  3 --- 6 --- 9 --- 12 --- 17 --- 21 --- 25
Level 0:  3 - 6 - 7 - 9 - 12 - 17 - 19 - 21 - 25 - 26
```

Level 0 is the full sorted list. Higher levels are shortcuts. A node that
appears on level 2 also appears on levels 1 and 0, forming a tower.

## Probabilistic Levels

When a new element is inserted, its tower height is chosen at random: start
at level 1, then keep adding a level while a coin flip comes up heads. With
probability `p = 1/2`, the chance of reaching level `k` is `(1/2)^(k-1)`.
That means about half the nodes reach level 1, a quarter reach level 2, an
eighth reach level 3, and so on. The maximum level is capped, commonly at
16 or 32, so a tower cannot grow without bound.

No rebalancing is ever needed. Insertions and deletions only touch the
tower of the affected element and its neighbors, and the random heights keep
the levels balanced in expectation. The tradeoff is that the structure is
randomized: an unlucky sequence of coin flips can produce a taller or
shorter tower than average, but the probability of a badly unbalanced list
is vanishingly small.

## Operations and Complexity

Search, insert, and delete all follow the same path: start at the top level
of the head node, move right while the next key is smaller than the target,
drop down, and repeat. Insertion splices the new tower into each level it
reaches; deletion removes it.

| Operation | Expected time | Worst-case time |
|---|---|---|
| Search | O(log n) | O(n) |
| Insert | O(log n) | O(n) |
| Delete | O(log n) | O(n) |

The expected number of steps is O(log n) because each level roughly halves
the remaining search space. The worst case is O(n), but it requires an
extremely unlucky set of coin flips and is not a practical concern.

Expected space is O(n). Each element stores one forward pointer per level
of its tower, and the expected tower height is 2 (since the sum of the
geometric series 1 + 1/2 + 1/4 + ... is 2), so the total number of pointers
is about 2n.

## Worked Search Example

Search for `17` in the list drawn above. Start at the head on level 3:

1. Level 3: head -> `3`, next is `21`, which is greater than `17`, so drop
   to level 2.
2. Level 2: at `3`, next is `9`, which is less than `17`, so move to `9`.
   Next is `17`, which is equal, so the search ends: `17` is found.
3. If the target were `19`, the search would drop to level 1 at `17`, move
   to `19` on level 0, and find it there.

Searching for `15` follows the same path, drops to level 0 at `12`, sees
that the next node is `17`, which is greater than `15`, and concludes that
`15` is not in the list. Only a handful of nodes were visited, not the
whole list.

## Comparison With Balanced Binary Search Trees

A balanced BST such as an AVL tree or a red-black tree guarantees O(log n)
in the worst case, while a skip list only guarantees it in expectation.
The skip list wins on simplicity: there are no rotations, no color bits,
and no balance invariants to maintain. Insertion is a local splice at each
level, which is easy to get right and easy to make lock-free for concurrent
use. The cost is extra memory for the forward pointers, and a small constant
factor from the random level choice.

Skip lists are used in practice where simplicity matters: Redis uses them
for sorted sets, and LevelDB uses them for its in-memory memtable.

## Key Takeaways

- A skip list adds sparse express lanes above a sorted linked list, so a search skips over large stretches instead of walking every node.
- Tower heights are random with p = 1/2, giving about half the nodes at level 1, a quarter at level 2, and so on.
- Search, insert, and delete are expected O(log n), with O(n) worst case that is vanishingly unlikely.
- Expected space is O(n), about two forward pointers per element on average.
- Skip lists trade the worst-case guarantee of a balanced BST for much simpler insertion and deletion, with no rotations.
