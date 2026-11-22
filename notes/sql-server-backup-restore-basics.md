# SQL Server Backup and Restore Basics

Backups are the only thing standing between a bad `DELETE` and a lost
business. SQL Server offers three backup types, and which ones you can take
depends on the database's **recovery model**. A backup you have never
restored is not a backup — it is a hope.

## Recovery Models

| Model | Log backups | Point-in-time restore | Log growth |
|-------|-------------|----------------------|------------|
| Simple | No | No (only to last backup) | Log reused automatically |
| Full | Yes | Yes | Log grows until backed up |
| Bulk-logged | Yes | Yes, except across bulk ops | Minimal logging for bulk loads |

- **Simple:** good for dev databases and data you can rebuild. The
  transaction log is truncated automatically at checkpoints.
- **Full:** the production default. Every change is logged, and log backups
  let you restore to any second.
- **Bulk-logged:** like Full, but `BULK INSERT` / `SELECT INTO` are minimally
  logged for speed. You lose point-in-time restore across those operations.

## The Three Backup Types

- **Full backup:** a complete copy of the database at that moment. The
  starting point for every restore chain.
- **Differential backup:** everything that changed since the last full
  backup. Smaller and faster than a full, but grows until the next full.
- **Transaction log backup:** all log records since the last log backup.
  Only available in Full and Bulk-logged models; this is what enables
  point-in-time restore.

A typical production schedule: full nightly, differential every few hours,
log every 15 minutes.

## BACKUP and RESTORE Syntax

```sql
-- Full backup
BACKUP DATABASE ShopDb
    TO DISK = 'D:\Backups\ShopDb_Full.bak'
    WITH INIT, COMPRESSION, CHECKSUM;

-- Differential backup
BACKUP DATABASE ShopDb
    TO DISK = 'D:\Backups\ShopDb_Diff.bak'
    WITH DIFFERENTIAL, COMPRESSION;

-- Transaction log backup
BACKUP LOG ShopDb
    TO DISK = 'D:\Backups\ShopDb_Log.trn'
    WITH COMPRESSION;
```

Restoring replays the chain in order — full, then the latest differential,
then log backups in sequence — and the database must stay in a restoring
state between steps (`NORECOVERY`) until the last one:

```sql
-- Restore the full backup, leaving the database ready for more restores
RESTORE DATABASE ShopDb
    FROM DISK = 'D:\Backups\ShopDb_Full.bak'
    WITH NORECOVERY;

-- Then the differential
RESTORE DATABASE ShopDb
    FROM DISK = 'D:\Backups\ShopDb_Diff.bak'
    WITH NORECOVERY;

-- Then log backups, in order, and bring the database online
RESTORE LOG ShopDb
    FROM DISK = 'D:\Backups\ShopDb_Log.trn'
    WITH RECOVERY;
```

Use `WITH MOVE` when the data/log files must land on different paths than
the original server, and `WITH REPLACE` only when you are sure you want to
overwrite the existing database.

## Point-in-Time Restore

In the Full recovery model you can stop the restore at an exact moment —
for example, just before someone ran a bad update at 14:32:

```sql
RESTORE DATABASE ShopDb FROM DISK = 'D:\Backups\ShopDb_Full.bak'
    WITH NORECOVERY;
RESTORE LOG ShopDb FROM DISK = 'D:\Backups\ShopDb_Log.trn'
    WITH STOPAT = '2026-11-21 14:32:00', RECOVERY;
```

For this to work you need an unbroken log chain: every log backup since the
full backup, in order, with no gaps. A single missing log backup breaks the
chain, and point-in-time restore becomes impossible past that gap.

## Verifying Backups

A backup file can be corrupt, incomplete, or taken from the wrong database.
Check before you need it:

```sql
-- Is the backup file readable and complete?
RESTORE VERIFYONLY FROM DISK = 'D:\Backups\ShopDb_Full.bak';

-- What is inside this backup file?
RESTORE HEADERONLY FROM DISK = 'D:\Backups\ShopDb_Full.bak';
RESTORE FILELISTONLY FROM DISK = 'D:\Backups\ShopDb_Full.bak';

-- Check the database itself for corruption
DBCC CHECKDB (ShopDb) WITH NO_INFOMSGS;
```

`VERIFYONLY` confirms the file is a valid, complete backup — it does not
prove the data inside is good. The only real proof is a **test restore**:
regularly restore to a spare server and run `DBCC CHECKDB` there. Automate
it; a manual "we'll test it someday" never happens.

## Common Mistakes

- Running log backups against a Simple-model database (fails — there is
  nothing to back up).
- Never backing up the log in Full model: the log file grows until the disk
  fills, and the database stops.
- Backing up to the same disk as the data files: a disk failure takes both.
- No `COPY_ONLY` on ad-hoc backups: an out-of-band full backup resets the
  differential base and breaks the scheduled chain. Use `WITH COPY_ONLY`
  for one-off backups.

## Key Takeaways

- Recovery model decides what is possible: Simple has no log backups or
  point-in-time restore; Full has both.
- A restore chain is full → latest differential → log backups in order,
  with `NORECOVERY` until the final `RECOVERY`.
- `STOPAT` restores to an exact moment, but only with an unbroken log chain.
- `RESTORE VERIFYONLY` checks the file; only a real test restore plus
  `DBCC CHECKDB` proves the backup works — schedule one.
