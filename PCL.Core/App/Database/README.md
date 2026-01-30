# Database Guildline
This is a light database management guildline for PCL.Core. It is not a strict rule, but a recommendation to follow.
## How to create a new database
1. You need create a class that inheritance from `DatabaseEntry`.
2. Then make a ctor that call `base(<connPath>)` with the connection path.
3. Now this class is ready to use. (Yes it is that simple, and will automatically create the database file if not exists)

## DatabaseEntry
The `DatabaseEntry` class is the entrance to use the database.
User must implement a class that inheritance from `DatabaseEntry` to use it.
And the child class should implement there own method to provide some ways to interact with the database.

## A Normal Example
```charp
public class UserDatabase : DatabaseEntry
{
	public UserDatabase() : base("example.db")
	{
		// You can do some initialization here.
		// Like create table if not exists,
		// or anything else.
	}


	public List<(int Id, string Name, int Age)> GetAllUsers()
	{
		// In your method, you shoul provide some way to interact with the database.
	}
}
```
Then, you can use it like this:
```charp
var db = new UserDatabase();
var users = db.GetAlUsers();
```