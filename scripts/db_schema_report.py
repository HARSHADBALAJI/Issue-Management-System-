"""
Database Schema Report Generator
Connects to the project's database (SQL Server or SQLite) and prints
all tables with columns, types, nullability, PKs, FKs, defaults, indexes.
"""

import os
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# 1. Load .env from the backend project directory
# ---------------------------------------------------------------------------
def load_dotenv(path):
    env = {}
    try:
        with open(path, encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#') or '=' not in line:
                    continue
                key, _, val = line.partition('=')
                env[key.strip()] = val.strip().strip('"').strip("'")
    except FileNotFoundError:
        pass
    return env

script_dir = Path(__file__).resolve().parent
project_root = script_dir.parent
backend_env = project_root / 'backend' / 'TicketSystem.Api' / '.env'

env = load_dotenv(backend_env)
for k, v in env.items():
    os.environ.setdefault(k, v)

DB_DRIVER   = os.environ.get('DB_DRIVER', 'sqlserver').lower()
DB_SERVER   = os.environ.get('DB_SERVER', 'localhost')
DB_DATABASE = os.environ.get('DB_DATABASE', 'ticketing_system')
DB_USER     = os.environ.get('DB_USERNAME') or ''
DB_PASS     = os.environ.get('DB_PASSWORD') or ''
DB_PORT     = os.environ.get('DB_PORT') or ''
SQLITE_FALLBACK = os.environ.get('USE_SQLITE_FALLBACK', 'false').lower() == 'true'

# ---------------------------------------------------------------------------
# 2. Connect
# ---------------------------------------------------------------------------
connection = None
db_type = None

if DB_DRIVER == 'sqlite' or SQLITE_FALLBACK:
    import sqlite3
    db_path = Path(DB_DATABASE) if Path(DB_DATABASE).suffix else Path(f'{DB_DATABASE}.db')
    if not db_path.is_absolute():
        db_path = project_root / 'backend' / 'TicketSystem.Api' / db_path
    print(f":: Connecting to SQLite: {db_path}")
    connection = sqlite3.connect(str(db_path))
    db_type = 'sqlite'
else:
    import pyodbc
    server_part = f"{DB_SERVER},{DB_PORT}" if DB_PORT else DB_SERVER
    if DB_USER:
        conn_str = f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server_part};DATABASE={DB_DATABASE};UID={DB_USER};PWD={DB_PASS};TrustServerCertificate=yes"
    else:
        conn_str = f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server_part};DATABASE={DB_DATABASE};Trusted_Connection=yes;TrustServerCertificate=yes"
    print(f":: Connecting to SQL Server: {server_part} / {DB_DATABASE}")
    connection = pyodbc.connect(conn_str)
    db_type = 'sqlserver'

connection.execute(f"USE [{DB_DATABASE}]")

cursor = connection.cursor()
SEP = '=' * 78
DASH = '-' * 78

# ---------------------------------------------------------------------------
# 3. Fetch metadata via sys schema (SQL Server) or PRAGMA (SQLite)
# ---------------------------------------------------------------------------
if db_type == 'sqlserver':
    tables_query = """
        SELECT s.name AS SCHEMA_NAME, t.name AS TABLE_NAME
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name NOT IN ('sys')
        ORDER BY s.name, t.name
    """
    cursor.execute(tables_query)
    tables = [(r[0], r[1]) for r in cursor.fetchall()]

    columns_query = """
        SELECT
            s.name       AS SCHEMA_NAME,
            t.name       AS TABLE_NAME,
            c.name       AS COLUMN_NAME,
            tp.name      AS DATA_TYPE,
            c.max_length,
            c.is_nullable,
            c.is_identity,
            dc.definition AS DEFAULT_DEF,
            c.precision,
            c.scale,
            c.column_id
        FROM sys.columns c
        JOIN sys.tables t        ON t.object_id  = c.object_id
        JOIN sys.schemas s       ON s.schema_id  = t.schema_id
        JOIN sys.types tp        ON tp.user_type_id = c.user_type_id
        LEFT JOIN sys.default_constraints dc
            ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE s.name NOT IN ('sys')
        ORDER BY s.name, t.name, c.column_id
    """
    cursor.execute(columns_query)
    col_rows = cursor.fetchall()

    pk_query = """
        SELECT
            s.name AS SCHEMA_NAME,
            t.name AS TABLE_NAME,
            c.name AS COLUMN_NAME
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        JOIN sys.tables t         ON t.object_id = i.object_id
        JOIN sys.schemas s        ON s.schema_id = t.schema_id
        WHERE i.is_primary_key = 1
        ORDER BY s.name, t.name, ic.key_ordinal
    """
    cursor.execute(pk_query)
    pk_rows = cursor.fetchall()

    fk_query = """
        SELECT
            s1.name  AS SCHEMA_NAME,
            t1.name  AS TABLE_NAME,
            c1.name  AS COLUMN_NAME,
            s2.name  AS REF_SCHEMA,
            t2.name  AS REF_TABLE,
            c2.name  AS REF_COLUMN,
            fk.name  AS FK_NAME
        FROM sys.foreign_key_columns fkc
        JOIN sys.foreign_keys fk      ON fk.object_id = fkc.constraint_object_id
        JOIN sys.columns c1           ON c1.object_id = fkc.parent_object_id AND c1.column_id = fkc.parent_column_id
        JOIN sys.tables t1            ON t1.object_id = fkc.parent_object_id
        JOIN sys.schemas s1           ON s1.schema_id = t1.schema_id
        JOIN sys.columns c2           ON c2.object_id = fkc.referenced_object_id AND c2.column_id = fkc.referenced_column_id
        JOIN sys.tables t2            ON t2.object_id = fkc.referenced_object_id
        JOIN sys.schemas s2           ON s2.schema_id = t2.schema_id
        WHERE s1.name NOT IN ('sys')
        ORDER BY s1.name, t1.name, fkc.constraint_column_id
    """
    cursor.execute(fk_query)
    fk_rows = cursor.fetchall()

    idx_query = """
        SELECT
            s.name  AS SCHEMA_NAME,
            t.name  AS TABLE_NAME,
            i.name  AS INDEX_NAME,
            i.is_primary_key,
            i.is_unique,
            c.name  AS COLUMN_NAME,
            ic.key_ordinal
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c        ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        JOIN sys.tables t         ON t.object_id = i.object_id
        JOIN sys.schemas s        ON s.schema_id = t.schema_id
        WHERE i.name IS NOT NULL AND s.name NOT IN ('sys')
        ORDER BY s.name, t.name, i.name, ic.key_ordinal
    """
    cursor.execute(idx_query)
    idx_rows = cursor.fetchall()

    ck_query = """
        SELECT
            s.name  AS SCHEMA_NAME,
            t.name  AS TABLE_NAME,
            cc.name AS CONSTRAINT_NAME,
            cc.definition AS CHECK_DEF
        FROM sys.check_constraints cc
        JOIN sys.tables t   ON t.object_id = cc.parent_object_id
        JOIN sys.schemas s  ON s.schema_id = t.schema_id
        WHERE s.name NOT IN ('sys')
    """
    cursor.execute(ck_query)
    ck_rows = cursor.fetchall()

    # Map check constraints to columns
    ck_cols_query = """
        SELECT
            s.name AS SCHEMA_NAME,
            t.name AS TABLE_NAME,
            c.name AS COLUMN_NAME,
            cc.name AS CONSTRAINT_NAME
        FROM sys.check_constraints cc
        JOIN sys.tables t   ON t.object_id = cc.parent_object_id
        JOIN sys.schemas s  ON s.schema_id = t.schema_id
        JOIN sys.columns c  ON c.object_id = cc.parent_object_id
            AND c.column_id = cc.parent_column_id
        WHERE s.name NOT IN ('sys')
    """
    try:
        cursor.execute(ck_cols_query)
        ck_col_rows = cursor.fetchall()
    except Exception:
        ck_col_rows = []

else:  # SQLite
    cursor.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
    tables = [('', r[0]) for r in cursor.fetchall() if r[0] != 'sqlite_sequence']

    col_rows = []
    fk_rows = []
    idx_rows = []
    ck_rows = []
    ck_col_rows = []
    pk_set = set()

    for schema, tname in tables:
        cursor.execute(f'PRAGMA table_info("{tname}")')
        for cid, cname, ctype, notnull, dflt, pk in cursor.fetchall():
            col_rows.append((schema, tname, cname, ctype, 0, notnull == 0, bool(pk), dflt, 0, 0, cid + 1))
            if pk:
                pk_set.add(f'{schema}.{tname}.{cname}')

        cursor.execute(f'PRAGMA foreign_key_list("{tname}")')
        for row in cursor.fetchall():
            fk_rows.append((schema, tname, row[3], '', row[2], row[4], f'FK_{tname}_{row[3]}'))

        cursor.execute(f'PRAGMA index_list("{tname}")')
        for idx in cursor.fetchall():
            idx_name, unique = idx[1], bool(idx[2])
            cursor.execute(f'PRAGMA index_info("{idx_name}")')
            for ci in cursor.fetchall():
                idx_rows.append((schema, tname, idx_name, 0, unique, ci[2], ci[0] + 1))

# ---------------------------------------------------------------------------
# 4. Build data structures
# ---------------------------------------------------------------------------
def build_type_str(dt, max_len, prec, scale):
    dt = dt.lower()
    if dt in ('varchar', 'nvarchar', 'char', 'nchar', 'varbinary'):
        if max_len == -1:
            return f'{dt}(max)'
        elif max_len and max_len > 0:
            # nvarchar/nchar store byte-pair lengths; divide by 2 for char count
            if dt.startswith('n'):
                return f'{dt}({max_len // 2})'
            return f'{dt}({max_len})'
    if dt in ('decimal', 'numeric') and prec:
        return f'{dt}({prec},{scale})'
    return dt

# PK lookup
pk_set = set()
for row in pk_rows:
    pk_set.add(f'{row[0]}.{row[1]}.{row[2]}')

# FK lookup
fk_map = {}
for row in fk_rows:
    key = f'{row[0]}.{row[1]}.{row[2]}'
    ref = f'{row[3]}.{row[4]}({row[5]})' if row[3] else f'{row[4]}({row[5]})'
    fk_map[key] = ref

# Index grouping
idx_map = {}
for row in idx_rows:
    key = f'{row[0]}.{row[1]}'
    idx_map.setdefault(key, []).append(row)

idx_grouped = {}
for key, rows in idx_map.items():
    merged = {}
    for row in rows:
        name = row[2]
        if name not in merged:
            merged[name] = {'name': name, 'is_primary': bool(row[3]), 'is_unique': bool(row[4]), 'columns': []}
        merged[name]['columns'].append((row[6], row[5]))
    for i in merged.values():
        i['columns'].sort(key=lambda x: x[0])
    idx_grouped[key] = list(merged.values())

# Check constraint grouping
ck_map = {}
for row in ck_rows:
    key = f'{row[0]}.{row[1]}'
    ck_map.setdefault(key, []).append({'constraint': row[2], 'definition': row[3]})

ck_col_map = {}
for row in ck_col_rows:
    key = f'{row[0]}.{row[1]}.{row[2]}'
    ck_col_map.setdefault(key, []).append(row[3])

# Columns per table
table_columns = {}
for row in col_rows:
    sch, tbl, col, dt, mx, isnullable, isid, dflt, prec, scale, ordinal = row[:11]
    key = f'{sch}.{tbl}' if sch else tbl
    table_columns.setdefault(key, []).append({
        'ordinal': ordinal,
        'name': col,
        'type': build_type_str(dt, mx, prec, scale),
        'nullable': bool(isnullable),
        'default': dflt,
        'is_pk': f'{sch}.{tbl}.{col}' in pk_set,
        'is_identity': bool(isid),
        'fk': fk_map.get(f'{sch}.{tbl}.{col}', ''),
    })
    table_columns[key].sort(key=lambda c: c['ordinal'])

# ---------------------------------------------------------------------------
# 5. Print report
# ---------------------------------------------------------------------------
print(f'\n{SEP}')
print(f'  DATABASE SCHEMA REPORT')
print(f'  Database : {DB_DATABASE}')
print(f'  Driver   : {DB_DRIVER.upper()}')
print(f'{SEP}')

for schema, tname in tables:
    key = f'{schema}.{tname}' if schema else tname
    cols = table_columns.get(key, [])
    if not cols:
        continue

    print(f'\n  -- TABLE: {tname} {"(" + schema + ")" if schema else ""}')
    print(f'  |  Columns: {len(cols)}')
    print(f'  ' + DASH[2:])
    print(f'  {"#":>3} {"Column":24} {"Type":28} {"Null":5} {"PK":3} {"ID":3}  Notes')
    print(f'  {"":->3} {"":->24} {"":->28} {"":->5} {"":->3} {"":->3}  {"":->30}')

    for c in cols:
        notes = []
        if c['default']:
            notes.append(f'DEFAULT {c["default"]}')
        if c['fk']:
            notes.append(f'FK -> {c["fk"]}')
        marker = f'  {c["ordinal"]:>3} {c["name"]:24} {c["type"]:28} {"Y" if c["nullable"] else "N":5} {"*" if c["is_pk"] else "":3} {"*" if c["is_identity"] else "":3}'
        if notes:
            marker += f'  {"; ".join(notes)}'
        print(marker)

    # Indexes
    indexes = idx_grouped.get(key, [])
    non_pk_idx = [i for i in indexes if not i['is_primary']]
    if non_pk_idx:
        print()
        for idx in non_pk_idx:
            cols_list = ', '.join(c[1] for c in idx['columns'])
            uniq = ' UNIQUE' if idx['is_unique'] else ''
            print(f'     INDEX{uniq}: {idx["name"]} ({cols_list})')

    # Check constraints
    checks = ck_map.get(key, [])
    if checks:
        print()
        for ck in checks:
            print(f'     CHECK: {ck["constraint"]}  ->  {ck["definition"]}')

    print()

connection.close()

total_cols = sum(len(v) for v in table_columns.values())
print(f'{SEP}')
print(f'  Report complete - {len(tables)} tables, {total_cols} columns')
print(f'{SEP}')
