# BitcraftChatListener

This is a simple Discord bot that listens to Bitcraft Chat, and then print the incoming stream to Discord.

## Usage

1. Run the app for the first time. It should generate a `config.json` you can fill up.
2. Fill in the `config.json`.
      * `DiscordToken`: Get it from https://discord.com/developers/applications, go to "Bot" and then click on "Reset Token" to get one.
	  * `DiscordClientId`: Get it from https://discord.com/developers/applications, find it under "General Information".
	  * `DiscordOutputGuild`: The ID of the server you want to output to. (Enable Developer Mode in Discord to show the option to get the ID)
	  * `DiscordOutputChannel`: The ID of the channel you want to output to. (Enable Developer Mode in Discord to show the option to get the ID)
	  * `SpacetimeDbAccessToken`: The identity JWT token you want to login with. You can get it from looking in PlayerPrefs for Bitcraft. Leave this blank to go through the login process in the app and it'll automatically save it for you and populate the configuration file with it.
	  * `SpacetimeDbName`: The name of the database you would like to access. Bitcraft's databases are `bitcraft-<region number>`, for example: `bitcraft-6` for Region 6.
	  * `SpacetimeDbLastAccessToken`: The last access token used. This was a debug value I forgot to remove.
	  * `OutputEverything`: Do you want to eavesdrop on the WHOLE server? You should leave this to `false`.
	  * `BitcraftRegionNumber`: Your Bitcraft region to filter messages for. Depending on your key, you may get everything, but a player access token will only return results for the region they are physically in.
3. Run `BitcraftChatListener.exe` again.