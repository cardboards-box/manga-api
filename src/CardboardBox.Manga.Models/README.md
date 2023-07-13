# General Purpose
This project is for any model used throughout the application life-cycle.
Here is a description of what each of the folders in this project are for as well as what to look out for.

## Base Models
Contains any abstract models that are used to indicate properties that should exist on any like object.
The `DbObject` class is the base class for all models that represent a table object in the database, and the properties are used in query generation.

## Table Models
Contains models that represent a table object in the database.
Ensure all table models have the `Table` attribute applied and are registered in the `CardboardBox.Manga.Database` project.

## Type Models
Contains models that represent a type object in the database.
Ensure all type models are registered in the `CardboardBox.Manga.Database` project (they don't have a special attribute)

## Composite Models
Contains models that don't represent a table object, but still represent the result of a query or the result of multiple return results of queries.
Ensure all composite models have the appropriate `Composite*` attribute applied. 
This helps ensure models are mapped correctly.
The attributes aren't actually used for anything other than documentation (I should probably have them auto-registered, but reflection...).

## Enums
Contains all of the enums that are present in the application.

## Request Models
Contains models that represent in-bound requests from either the API or the bot, separated by parent objects or type.

