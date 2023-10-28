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
