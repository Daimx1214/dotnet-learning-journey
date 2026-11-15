// TrieExamples.cs
// A Trie (prefix tree) stores words character by character, so words that share
// a prefix share nodes. Insert, Search, and StartsWith all run in O(m) time,
// where m is the length of the word — independent of how many words are stored.
// Run with: dotnet run

var trie = new Trie();

string[] words = { "apple", "app", "application", "apply", "banana", "band", "bandana" };
foreach (string word in words)
    trie.Insert(word);

Console.WriteLine("Inserted: " + string.Join(", ", words));
Console.WriteLine();

Console.WriteLine("Search(\"app\"):         " + trie.Search("app"));         // True — inserted as a word
Console.WriteLine("Search(\"apple\"):       " + trie.Search("apple"));       // True
Console.WriteLine("Search(\"appl\"):        " + trie.Search("appl"));        // False — prefix only, not a word
Console.WriteLine("Search(\"banana\"):      " + trie.Search("banana"));      // True
Console.WriteLine("Search(\"bandana\"):     " + trie.Search("bandana"));     // True
Console.WriteLine("Search(\"orange\"):      " + trie.Search("orange"));      // False — never inserted
Console.WriteLine("Search(\"band\"):        " + trie.Search("band"));        // True
Console.WriteLine();

Console.WriteLine("StartsWith(\"app\"):     " + trie.StartsWith("app"));     // True
Console.WriteLine("StartsWith(\"ban\"):     " + trie.StartsWith("ban"));     // True
Console.WriteLine("StartsWith(\"ora\"):     " + trie.StartsWith("ora"));     // False
Console.WriteLine();

Console.WriteLine("Words starting with \"app\":  " + string.Join(", ", trie.GetWordsWithPrefix("app")));
Console.WriteLine("Words starting with \"ban\":  " + string.Join(", ", trie.GetWordsWithPrefix("ban")));
Console.WriteLine("Words starting with \"ora\":  " + string.Join(", ", trie.GetWordsWithPrefix("ora")));
Console.WriteLine("Words starting with \"\":     " + string.Join(", ", trie.GetWordsWithPrefix("")));

// A node in the Trie. Each node holds one character's children plus a flag
// marking the end of a complete word ("app" is a word and also a prefix of
// "apple", so the node for its final 'p' has IsEndOfWord = true AND children).
public class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; } = new();
    public bool IsEndOfWord { get; set; }
}

public class Trie
{
    private readonly TrieNode _root = new();

    public void Insert(string word)
    {
        TrieNode node = _root;
        foreach (char c in word)
        {
            if (!node.Children.TryGetValue(c, out TrieNode? child))
            {
                child = new TrieNode();
                node.Children[c] = child;
            }
            node = child;
        }
        node.IsEndOfWord = true;
    }

    public bool Search(string word)
    {
        TrieNode? node = FindNode(word);
        return node is not null && node.IsEndOfWord;
    }

    public bool StartsWith(string prefix) => FindNode(prefix) is not null;

    // Returns every stored word that begins with the given prefix,
    // in alphabetical order (children are visited sorted by character).
    public List<string> GetWordsWithPrefix(string prefix)
    {
        var results = new List<string>();
        TrieNode? node = FindNode(prefix);
        if (node is not null)
            CollectWords(node, prefix, results);
        return results;
    }

    // Walks down the tree following the characters; returns null if any
    // character is missing. Shared by Search, StartsWith, and GetWordsWithPrefix.
    private TrieNode? FindNode(string text)
    {
        TrieNode node = _root;
        foreach (char c in text)
        {
            if (!node.Children.TryGetValue(c, out TrieNode? child))
                return null;
            node = child;
        }
        return node;
    }

    private static void CollectWords(TrieNode node, string current, List<string> results)
    {
        if (node.IsEndOfWord)
            results.Add(current);

        foreach (char c in node.Children.Keys.OrderBy(c => c))
            CollectWords(node.Children[c], current + c, results);
    }
}
