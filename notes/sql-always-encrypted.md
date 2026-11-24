# SQL Server Always Encrypted

Always Encrypted encrypts sensitive columns inside SQL Server so that plaintext is never visible to the database engine, DBAs, or anyone with access to backups. Encryption and decryption happen in the client driver (.NET, JDBC, ODBC) using keys the server never sees. It is the right tool when the threat model includes the database administrator or a stolen backup; it is the wrong tool when the application itself needs to search or compute over the encrypted values.

## The Key Hierarchy

Two keys are involved. The **column master key (CMK)** lives outside SQL Server — in Windows Certificate Store, Azure Key Vault, or a hardware security module — and it encrypts the **column encryption key (CEK)**. The CEK is stored in the database (encrypted by the CMK) and does the actual column encryption. The driver fetches the encrypted CEK from the database, asks the key store to decrypt it with the CMK, and then uses the CEK to encrypt or decrypt values. SQL Server itself never holds the CMK or the plaintext CEK.

```sql
-- 1. Column master key: points at a certificate in the key store.
CREATE COLUMN MASTER KEY MyCMK
WITH (
    KEY_STORE_PROVIDER_NAME = 'MSSQL_CERTIFICATE_STORE',
    KEY_PATH = 'CurrentUser/My/4A3B8F2C1D5E6A7B8C9D0E1F2A3B4C5D6E7F8A9B'
);

-- 2. Column encryption key: generated and encrypted by the CMK.
CREATE COLUMN ENCRYPTION KEY MyCEK
WITH VALUES (
    COLUMN_MASTER_KEY = MyCMK,
    ALGORITHM = 'RSA_OAEP',
    ENCRYPTED_VALUE = 0x016E000001630075007200720065006E00740055007300650072002F004D0079002F00...
);

-- 3. Encrypt a column with the CEK (deterministic here so it can be joined on).
CREATE TABLE Customers (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100),
    NationalId NVARCHAR(20)
        COLLATE Latin1_General_BIN2
        ENCRYPTED WITH (
            COLUMN_ENCRYPTION_KEY = MyCEK,
            ENCRYPTION_TYPE = DETERMINISTIC,
            ALGORITHM = 'AEAD_AES_256_CBC_HMAC_SHA_256')
);
```

In practice, keys are created with SQL Server Management Studio's Always Encrypted wizard or PowerShell (`New-SqlColumnMasterKey`, `New-SqlColumnEncryptionKey`), not by hand-writing the binary `ENCRYPTED_VALUE`. The wizard also handles key rotation: create a new CEK, re-encrypt the column, drop the old CEK.

## Deterministic vs Randomized Encryption

**Deterministic** encryption always produces the same ciphertext for the same plaintext, so equality searches, joins, and `GROUP BY` work. It leaks equality patterns — an attacker who sees two identical ciphertexts knows the plaintexts are equal — so use it only for columns that must be searchable, like an ID number used in lookups.

**Randomized** encryption uses a random initialization vector, so the same plaintext encrypts differently every time. It is more secure but supports no operations at all: no equality, no range, no sorting, no grouping. Use it for columns that are only ever read back whole, like a credit card number displayed once at checkout.

| Operation | Deterministic | Randomized |
|---|---|---|
| Equality (`=`, `JOIN`, `GROUP BY`) | Yes | No |
| Range (`<`, `>`, `BETWEEN`) | No | No |
| `LIKE`, sorting, computed columns | No | No |
| Read back full value | Yes | Yes |

## Driver Behavior in .NET

The .NET driver (`Microsoft.Data.SqlClient`) performs encryption transparently when `Column Encryption Setting=Enabled` is in the connection string. Parameterized queries are required: the driver encrypts parameter values before sending them, and the server compares ciphertext to ciphertext. A literal value in the SQL text is sent in plaintext and fails with an operand type clash error.

```csharp
// Connection string must enable column encryption.
string cs = "Server=.;Database=Shop;Integrated Security=true;" +
            "Column Encryption Setting=Enabled";

await using var conn = new SqlConnection(cs);
await conn.OpenAsync();

// Parameters are encrypted by the driver before reaching the server.
await using var cmd = new SqlCommand(
    "SELECT Name FROM Customers WHERE NationalId = @id", conn);
cmd.Parameters.AddWithValue("@id", "35202-1234567-1"); // encrypted client-side
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
    Console.WriteLine(reader.GetString(0));

// This FAILS: a literal is not encrypted by the driver.
// SELECT Name FROM Customers WHERE NationalId = '35202-1234567-1'
```

Entity Framework Core supports Always Encrypted through the same driver setting, but LINQ queries that compare encrypted columns must use parameters (which EF Core does by default) — a `.Where(c => c.NationalId == someVariable)` works, while `FromSqlRaw` with an inline literal does not.

## Limits

- Columns must use a `_BIN2` collation for deterministic encryption, and the column type must be one of the supported types (no `xml`, `geography`, `timestamp`, `sql_variant`, or user-defined types).
- Encrypted columns cannot be part of a full-text index, cannot be a primary key or foreign key (deterministic columns can be indexed, but not keys in some configurations), and cannot use `IDENTITY` or computed-column definitions.
- Encryption adds roughly 30–40 bytes of overhead per value (IV + authentication tag), and deterministic encryption prevents index seeks from being as efficient as plaintext seeks.
- `tempdb` cannot hold encrypted data in the same way, so `#temp` tables with encrypted columns require the same keys and settings.
- Always Encrypted does not protect data from the application itself: any code with the connection string and access to the CMK can decrypt. It protects against the DBA, the backup thief, and the compromised server — not against a compromised app.

## Common Pitfalls

- Forgetting `Column Encryption Setting=Enabled` produces confusing "operand type clash" errors instead of a clear message.
- Using literals instead of parameters in queries against encrypted columns — always parameterize.
- Encrypting a column and then trying to run `LIKE '%...%'` or range queries on it: the server cannot evaluate them and the query fails.
- Losing the CMK certificate: without it the data is unrecoverable, full stop. Back up the certificate and its private key to a secure location separate from the database backups.
- Rotating the CEK without re-encrypting: old data stays encrypted with the old CEK, so both keys must be retained until every row is re-encrypted.

## Key Takeaways

- Always Encrypted encrypts columns in the client driver; SQL Server, DBAs, and backup thieves never see plaintext.
- The CMK (in a certificate store or Key Vault) encrypts the CEK (stored in the database); losing the CMK means losing the data.
- Deterministic encryption supports equality, joins, and grouping but leaks equality patterns; randomized supports no operations at all.
- The driver needs `Column Encryption Setting=Enabled` and parameterized queries — literals against encrypted columns fail.
- Encrypted columns cannot be searched with `LIKE` or ranges, cannot use most special types, and add ~30–40 bytes per value.
