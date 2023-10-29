# sqlite implementation in csharp
A barebones SQLite implementation. Goal is to support basic queries like SELECT directly on the raw file without any libraries used.

startpoint was [codecrafters.io](https://codecrafters.io) though i want to write my own unit-tests to check for validation along the steps instead using their unit testing pipeline.


# Steps

## make reading db info out possible
```
$ CS_Sqlite sample.db .dbinfo
```
## print number of tables
`.dbinfro` should read out the total number of tables
```
$ CS_Sqlite sample.db .dbinfo

database page size:  4096
write format:        1
read format:         1

...

number of tables:    5
schema size:         330
data version:        1
```
### resources
- https://www.sqlite.org/fileformat.html#storage_of_the_sql_database_schema
- https://www.sqlite.org/schematab.html
- https://fly.io/blog/sqlite-internals-btree/
- https://link.springer.com/content/pdf/10.1007/978-3-030-98467-0_5.pdf
- https://saveriomiroddi.github.io/SQLIte-database-file-format-diagrams/


## print table names
```
$ ./CS_Sqlite sample.db .tables
```
- should print out names of all tables

## Count rows in a table
```
$ ./CS_Sqlite "SELECT COUNT(*) FROM apples"

```
- should count the ammount of rows from a table

### notes
rootpage stores page number of root b-tree page for tables and indexes.

Page number is 1-indexed so to get the offset we need to subtract page_number - 1

## Read data from a single column
```
$ ./CS_Sqlite sample.db "SELECT name FROM apples"

```

## Read data from multiple columns
```
$ ./CS_Sqlite sample.db "SELECT name, color FROM apples"
```

## Filter data with a where clause
```
$ ./CS_Sqlite sample.db "SELECT name, color FROM apples WHERE color = 'Yellow'"
```
- implement it for one page first
## Retreive data using a full-table scan
```
$ ./CS_Sqlite superheroes.db "SELECT id, name FROM superheroes WHERE eye_color = 'Pink Eyes'"

```
- now implement it handling multiple pages

### resources
- traversing a b-tree https://medium.com/basecs/busying-oneself-with-b-trees-78bbf10522e7

## Retrieve data using an index
```
$ ./CS_Sqlite companies.db "SELECT id, name FROM companies WHERE country = 'eritrea'"
```
- implement index scanning. Rather than reading all rows and then filtering in memory the programm should directly search more intelligent.



## notes about db-file structure
the file starts with a header part that is exactly 100 bytes long.
- Beyond this the file is divided into pages of equal size. (default 4096 bytes).
- each page (besides first) stores data regarding exactly one table

### byte flags
The first byte contains info about the page-type this page is.

|page type| 1st byte|extra info|
|---|---|---|
|table b-tree interior page|0x05||
|table b-tree leaf page|0x0d||
|index b-tree interior page|0x02||
|index b-tree leaf page|0x0a||
|overflow|page|0x00|for db size ´<´ 64GB|
|freelist page|0x00|first 8 bytes filled with zero bytes|
|pointer map|0x01-0x05||
|locking page| 0x00|only if db-size ´>´ 1GB|

### the first page
- the header **is part** of the first page.
- the first page contains the database schema.
- like the SQLite_Master-Table, with info like:
  - root page numbers
  - column names
  - column types

### Record format
- The naive (bad) approach of a database would be to just pack records in sequentially. 
This is obviously a terrible idea for a dynamically growing/shrinking data structure.
- Instead SQLite will pack data into chunks. Default 4KB, more exact: 4096byte. 
  - This allows chunks to stay inside file-system boundaries
  - and so it can read/write/update only those chunks it needs to.
### page format
when inspecting data directly on the first page we encounter the leaf-table-header:
```
0D-00-00-00-03-0E-C3-00
```
- 0x0d indicates he page-type. This **first** page is a **table leaf**
- 0x0003 is the cell count. This tells us that 3 records exist in this page. 

Directly after this leaf-table-header we encounter the first cell pointer index:
```
0F-8F-0F-3D-0E-C3
```
- this is a list of 2byte values representing offsets in the page for each record:
  - 0x0F8F = 3982
  - 0x0F3D = 3901
  - 0x0EC3 = 3779
- Sqlite starts to fill these chunks from the back.

### binary tree
Sqlite uses a `a b+tree`. 