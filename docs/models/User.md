# User
This is the user model for melon.
# Model
|Type|Name|Notes|
|----|----|----|
|ObjectId|\_id|MongoDb's BSON ID|
|string|UserId|The hex string representation of the user's ID|
|string|Username|The username of the user|
|string|Password|The hash of the password of the user|
|string|Bio|The user's bio|
|byte[]|Salt|The salt used to hash the password|
|string|Type|The type of user (Admin, User, Pass)|
|string|FavTrack|A track ID for the user's favorite track|
|string|FavAlbum|An album ID for the user's favorite album|
|string|FavArtist|An artist ID for the user's favorite artist|
|bool|PublicStats|A bool representing if the user's stats and profile should be publicly viewable|
|DateTime|LastLogin|The last login date and time|
