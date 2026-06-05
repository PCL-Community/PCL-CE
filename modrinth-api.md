# Modrinth API Documentation

# Overview

## Labrinth (v2.7.0/366f528)

This documentation doesn't provide a way to test our API. In order to facilitate testing, we recommend the following tools:

- cURL (recommended, command-line)
- ReqBIN (recommended, online)
- Postman
- Insomnia
- Your web browser, if you don't need to send headers or a request body

Once you have a working client, you can test that it works by making a GET request to https://staging-api.modrinth.com/:

```
{
  "about": "Welcome traveler!",
  "documentation": "https://docs.modrinth.com",
  "name": "modrinth-labrinth",
  "version": "2.7.0"
}
```

If you got a response similar to the one above, you can use the Modrinth API!
When you want to go live using the production API, use api.modrinth.com instead of staging-api.modrinth.com.

## Authentication

This API has two options for authentication: personal access tokens and OAuth2.
All tokens are tied to a Modrinth user and use the Authorization header of the request.

Example:

```
Authorization: mrp_RNtLRSPmGj2pd1v1ubi52nX7TJJM9sznrmwhAuj511oe4t1jAqAQ3D6Wc8Ic
```

You do not need a token for most requests. Generally speaking, only the following types of requests require a token:

- those which create data (such as version creation)
- those which modify data (such as editing a project)
- those which access private data (such as draft projects, notifications, emails, and payout data)

Each request requiring authentication has a certain scope. For example, to view the email of the user being requested, the token must have the USER_READ_EMAIL scope.
You can find the list of available scopes on GitHub. Making a request with an invalid scope will return a 401 error.

Please note that certain scopes and requests cannot be completed with a personal access token or using OAuth.
For example, deleting a user account can only be done through Modrinth's frontend.

A detailed guide on OAuth has been published in Modrinth's technical documentation.

### Personal access tokens

Personal access tokens (PATs) can be generated in from the user settings.

### GitHub tokens

For backwards compatibility purposes, some types of GitHub tokens also work for authenticating a user with Modrinth's API, granting all scopes.
We urge any application still using GitHub tokens to start using personal access tokens for security and reliability purposes.
GitHub tokens will cease to function to authenticate with Modrinth's API as soon as version 3 of the API is made generally available.

## Cross-Origin Resource Sharing

This API features Cross-Origin Resource Sharing (CORS) implemented in compliance with the W3C spec.
This allows for cross-domain communication from the browser.
All responses have a wildcard same-origin which makes them completely public and accessible to everyone, including any code on any site.

## Identifiers

The majority of items you can interact with in the API have a unique eight-digit base62 ID.
Projects, versions, users, threads, teams, and reports all use this same way of identifying themselves.
Version files use the sha1 or sha512 file hashes as identifiers.

Each project and user has a friendlier way of identifying them; slugs and usernames, respectively.
While unique IDs are constant, slugs and usernames can change at any moment.
If you want to store something in the long term, it is recommended to use the unique ID.

## Ratelimits

The API has a ratelimit defined per IP. Limits and remaining amounts are given in the response headers.

- X-Ratelimit-Limit: the maximum number of requests that can be made in a minute
- X-Ratelimit-Remaining: the number of requests remaining in the current ratelimit window
- X-Ratelimit-Reset: the time in seconds until the ratelimit window resets

Ratelimits are the same no matter whether you use a token or not.
The ratelimit is currently 300 requests per minute. If you have a use case requiring a higher limit, please contact us.

## User Agents

To access the Modrinth API, you must use provide a uniquely-identifying User-Agent header.
Providing a user agent that only identifies your HTTP client library (such as "okhttp/4.9.3") increases the likelihood that we will block your traffic.
It is recommended, but not required, to include contact information in your user agent.
This allows us to contact you if we would like a change in your application's behavior without having to block your traffic.

- Bad: User-Agent: okhttp/4.9.3
- Good: User-Agent: project_name
- Better: User-Agent: github_username/project_name/1.56.0
- Best: User-Agent: github_username/project_name/1.56.0 (launcher.com) or User-Agent: github_username/project_name/1.56.0 (contact@launcher.com)

## Versioning

Modrinth follows a simple pattern for its API versioning.
In the event of a breaking API change, the API version in the URL path is bumped, and migration steps will be published below.

When an API is no longer the current one, it will immediately be considered deprecated.
No more support will be provided for API versions older than the current one.
It will be kept for some time, but this amount of time is not certain.

We will exercise various tactics to get people to update their implementation of our API.
One example is by adding something like STOP USING THIS API to various data returned by the API.

Once an API version is completely deprecated, it will permanently return a 410 error.
Please ensure your application handles these 410 errors.

### Migrations

Inside the following spoiler, you will be able to find all changes between versions of the Modrinth API, accompanied by tips and a guide to migrate applications to newer versions.

Here, you can also find changes for Minotaur, Modrinth's official Gradle plugin. Major versions of Minotaur directly correspond to major versions of the Modrinth API.

These bullet points cover most changes in the v2 API, but please note that fields containing mod in most contexts have been shifted to project. For example, in the search route, the field mod_id was renamed to project_id.

- The search route has been moved from /api/v1/mod to /v2/search
- New project fields: project_type (may be mod or modpack), moderation_message (which has a message and body), gallery
- New search facet: project_type
- Alphabetical sort removed (it didn't work and is not possible due to limits in MeiliSearch)
- New search fields: project_type, gallery
- The gallery field is an array of URLs to images that are part of the project's gallery
- The gallery is a new feature which allows the user to upload images showcasing their mod to the CDN which will be displayed on their mod page
- Internal change: Any project file uploaded to Modrinth is now validated to make sure it's a valid Minecraft mod, Modpack, etc.
- In project creation, projects may not upload a mod with no versions to review, however they can be saved as a draft
- Donation URLs have been enabled
- New project status: archived. Projects with this status do not appear in search
- Tags (such as categories, loaders) now have icons (SVGs) and specific project types attached
- Dependencies have been wiped and replaced with a new system
- Notifications now have a type field, such as project_update

Along with this, project subroutes (such as /v2/project/{id}/version) now allow the slug to be used as the ID. This is also the case with user routes.

Minotaur 2.x introduced a few breaking changes to how your buildscript is formatted.

First, instead of registering your own publishModrinth task, Minotaur now automatically creates a modrinth task. As such, you can replace the task publishModrinth(type: TaskModrinthUpload) { line with just modrinth {.

To declare supported Minecraft versions and mod loaders, the gameVersions and loaders arrays must now be used. The syntax for these are pretty self-explanatory.

Instead of using releaseType, you must now use versionType. This was actually changed in v1.2.0, but very few buildscripts have moved on from v1.1.0.

Dependencies have been changed to a special DSL. Create a dependencies block within the modrinth block, and then use scope.type("project/version"). For example, required.project("fabric-api") adds a required project dependency on Fabric API.

You may now use the slug anywhere that a project ID was previously required.

- Modrinth Support: https://support.modrinth.com - support@modrinth.com
- Terms of Service
- OpenAPI version: 3.0.0

## Authentication

### TokenAuth

Security scheme type: apiKey

Header parameter name: Authorization

---

## Projects

### Tags

## projects

Projects are what Modrinth is centered around, be it mods, modpacks, resource packs, etc.

# Search projects

- Production server
- Staging server

## Parameters

### Query Parameters

- **query** (string): stringgravestonesThe query to search for
- **facets** (string): stringFacets are an essential concept for understanding how to filter out results. These are the most commonly used facet types: project_type categories (loaders are lumped in with categories in search)
- **index** (string): string default: relevance Allowed values: relevance downloads follows newest updated downloadsThe sorting method used for sorting search results
- **offset** (integer): integer20The offset into the search. Skips this number of results
- **limit** (integer): integer default: 10 <= 100 20The number of results returned by the search

## Responses

### 200

Expected response to a valid request

- **hits** (Array): The list of resultsArray object slug The slug of a project, used for vanity URLs. Regex: ^[\\w!@$()`.+,"\-']{3,64}$stringmy_project title The title or name of the projectstringMy Project description A
- **slug** (string): The slug of a project, used for vanity URLs. Regex: ^[\\w!@$()`.+,"\-']{3,64}$stringmy_project
- **title** (string): The title or name of the projectstringMy Project
- **description** (string): A short description of the projectstringA short description
- **categories** (Array<string>): A list of the categories that the project hasArray<string>[ "technology", "adventure", "fabric"]
- **client_side** (string): The client side support of the projectstringAllowed values: required optional unsupported unknown required
- **server_side** (string): The server side support of the projectstringAllowed values: required optional unsupported unknown optional
- **project_type** (string): The project type of the projectstringAllowed values: mod modpack resourcepack shader mod
- **downloads** (integer): The total number of downloads of the projectinteger
- **icon_url** (string): The URL of the project's iconstring nullable https://cdn.modrinth.com/data/AABBCCDD/b46513nd83hb4792a9a0e1fn28fgi6090c1842639.png
- **color** (integer): The RGB color of the project, automatically generated from the project iconinteger nullable 8703084
- **thread_id** (string): The ID of the moderation thread associated with this projectstringTTUUVVWW
- **monetization_status** (string): stringAllowed values: monetized demonetized force-demonetized
- **project_id** (string): The ID of the projectstringAABBCCDD
- **author** (string): The username of the project's authorstringmy_user
- **display_categories** (Array<string>): A list of the categories that the project has which are not secondaryArray<string>[ "technology", "fabric"]
- **versions** (Array<string>): A list of the minecraft versions supported by the projectArray<string>[ "1.8", "1.8.9"]
- **follows** (integer): The total number of users following the projectinteger
- **date_created** (string): The date the project was added to searchstring format: ISO-8601
- **date_modified** (string): The date the project was last modifiedstring format: ISO-8601
- **latest_version** (string): The latest version of minecraft that this project supportsstring1.8.9
- **license** (string): The SPDX license ID of a projectstringMIT
- **gallery** (Array<string>): All gallery images attached to the projectArray<string>[ "https://cdn.modrinth.com/data/AABBCCDD/images/009b7d8d6e8bf04968a29421117c59b3efe2351a.png", "https://cdn.modrinth.com/data/AABBCCDD/images/c2
- **featured_gallery** (string): The featured gallery image of the projectstring nullable
- **offset** (integer): The number of results that were skipped by the queryinteger0
- **limit** (integer): The number of results that were returned by the queryinteger10
- **total_hits** (integer): The total number of results that match the queryinteger10

### 400

Request was invalid, see given error

- **error** (string): The name of the errorstringinvalid_input
- **description** (string): The contents of the errorstringError while parsing multipart payload

# Get a project

- Production server
- Staging server

## Parameters

### Path Parameters

- **id|slug** (string): string[ "AABBCCDD", "my_project"]The ID or slug of the project

## Responses

### 200

Expected response to a valid request

- **slug** (string): The slug of a project, used for vanity URLs. Regex: ^[\\w!@$()`.+,"\-']{3,64}$stringmy_project
- **title** (string): The title or name of the projectstringMy Project
- **description** (string): A short description of the projectstringA short description
- **categories** (Array<string>): A list of the categories that the project hasArray<string>[ "technology", "adventure", "fabric"]
- **client_side** (string): The client side support of the projectstringAllowed values: required optional unsupported unknown required
- **server_side** (string): The server side support of the projectstringAllowed values: required optional unsupported unknown optional
- **body** (string): A long form description of the projectstringA long body describing my project in detail
- **status** (string): The status of the projectstringAllowed values: approved archived rejected draft unlisted processing withheld scheduled private unknown approved
- **requested_status** (string): The requested status when submitting for review or scheduling the project for releasestring nullable Allowed values: approved archived unlisted private draft
- **additional_categories** (Array<string>): A list of categories which are searchable but non-primaryArray<string>[ "technology", "adventure", "fabric"]
- **issues_url** (string): An optional link to where to submit bugs or issues with the projectstring nullable https://github.com/my_user/my_project/issues
- **source_url** (string): An optional link to the source code of the projectstring nullable https://github.com/my_user/my_project
- **wiki_url** (string): An optional link to the project's wiki page or other relevant informationstring nullable https://github.com/my_user/my_project/wiki
- **discord_url** (string): An optional invite link to the project's discordstring nullable https://discord.gg/AaBbCcDd
- **donation_urls** (Array<object>): A list of donation links for the projectArray<object> object id The ID of the donation platformstringpatreon platform The donation platform this link is tostringPatreon url The URL of the donation pla
- **id** (string): The ID of the donation platformstringpatreon
- **platform** (string): The donation platform this link is tostringPatreon
- **url** (string): The URL of the donation platform and userstringhttps://www.patreon.com/my_user
- **slug** (string): The slug of a project, used for vanity URLs. Regex: ^[\\w!@$()`.+,"\-']{3,64}$stringmy_project
- **title** (string): The title or name of the projectstringMy Project
- **description** (string): A short description of the projectstringA short description
- **categories** (Array<string>): A list of the categories that the project hasArray<string>[ "technology", "adventure", "fabric"]
- **client_side** (string): The client side support of the projectstringAllowed values: required optional unsupported unknown required
- **server_side** (string): The server side support of the projectstringAllowed values: required optional unsupported unknown optional
- **project_type** (string): The project type of the projectstringAllowed values: mod modpack resourcepack shader mod
- **downloads** (integer): The total number of downloads of the projectinteger
- **icon_url** (string): The URL of the project's iconstring nullable https://cdn.modrinth.com/data/AABBCCDD/b46513nd83hb4792a9a0e1fn28fgi6090c1842639.png
- **color** (integer): The RGB color of the project, automatically generated from the project iconinteger nullable 8703084
- **thread_id** (string): The ID of the moderation thread associated with this projectstringTTUUVVWW
- **monetization_status** (string): stringAllowed values: monetized demonetized force-demonetized
- **id** (string): The ID of the project, encoded as a base62 stringstringAABBCCDD
- **team** (string): The ID of the team that has ownership of this projectstringMMNNOOPP
- **body_url** (string): The link to the long description of the project. Always null, only kept for legacy compatibility.string nullable
- **moderator_message** (object): A message that a moderator sent regarding the project object message The message that a moderator has left for the projectstring body The longer body of the message that a moderator has left for the p
- **message** (string): The message that a moderator has left for the projectstring
- **body** (string): The longer body of the message that a moderator has left for the projectstring nullable
- **published** (string): The date the project was publishedstring format: ISO-8601
- **updated** (string): The date the project was last updatedstring format: ISO-8601
- **approved** (string): The date the project's status was set to an approved statusstring format: ISO-8601 nullable
- **queued** (string): The date the project's status was submitted to moderators for reviewstring format: ISO-8601 nullable
- **followers** (integer): The total number of users following the projectinteger
- **license** (object): The license of the project object id The SPDX license ID of a projectstringLGPL-3.0-or-later name The long name of a licensestringGNU Lesser General Public License v3 or later url The URL to this lice
- **id** (string): The SPDX license ID of a projectstringLGPL-3.0-or-later
- **name** (string): The long name of a licensestringGNU Lesser General Public License v3 or later
- **url** (string): The URL to this licensestring nullable
- **versions** (Array<string>): A list of the version IDs of the project (will never be empty unless draft status)Array<string>[ "IIJJKKLL", "QQRRSSTT"]
- **game_versions** (Array<string>): A list of all of the game versions supported by the projectArray<string>[ "1.19", "1.19.1", "1.19.2", "1.19.3"]
- **loaders** (Array<string>): A list of all of the loaders supported by the projectArray<string>[ "forge", "fabric", "quilt"]
- **gallery** (Array<object>): A list of images that have been uploaded to the project's galleryArray<object> object url required The URL of the gallery imagestringhttps://cdn.modrinth.com/data/AABBCCDD/images/009b7d8d6e8bf04968a29
- **url** (string): The URL of the gallery imagestringhttps://cdn.modrinth.com/data/AABBCCDD/images/009b7d8d6e8bf04968a29421117c59b3efe2351a.png
- **featured** (boolean): Whether the image is featured in the gallerybooleantrue
- **title** (string): The title of the gallery imagestring nullable My awesome screenshot!
- **description** (string): The description of the gallery imagestring nullable This awesome screenshot shows all of the blocks in my mod!
- **created** (string): The date and time the gallery image was createdstring format: ISO-8601
- **ordering** (integer): The order of the gallery image. Gallery images are sorted by this field and then alphabetically by title.integer0

### 404

The requested item(s) were not found or no authorization to access the requested item(s)

# Delete a project

- Production server
- Staging server

## Authorizations

- TokenAuth PROJECT_DELETE

## Parameters

### Path Parameters

- **id|slug** (string): string[ "AABBCCDD", "my_project"]The ID or slug of the project

## Responses

### 204

Expected response to a valid request

### 400

Request was invalid, see given error

- **error** (string): The name of the errorstringinvalid_input
- **description** (string): The contents of the errorstringError while parsing multipart payload

### 401

Incorrect token scopes or no authorization to access the requested item(s)

- **error** (string): The name of the errorstringunauthorized
- **description** (string): The contents of the errorstringAuthentication Error: Invalid Authentication Credentials

# Modify a project

- Production server
- Staging server

## Authorizations

- TokenAuth PROJECT_WRITE

## Parameters

### Path Parameters

- **id|slug** (string): string[ "AABBCCDD", "my_project"]The ID or slug of the project

## Request Body

Modified project fields

- **slug** (string): The slug of a project, used for vanity URLs. Regex: ^[\\w!@$()`.+,"\-']{3,64}$stringmy_project
- **title** (string): The title or name of the projectstringMy Project
- **description** (string): A short description of the projectstringA short description
- **categories** (Array<string>): A list of the categories that the project hasArray<string>[ "technology", "adventure", "fabric"]
- **client_side** (string): The client side support of the projectstringAllowed values: required optional unsupported unknown required
- **server_side** (string): The server side support of the projectstringAllowed values: required optional unsupported unknown optional
- **body** (string): A long form description of the projectstringA long body describing my project in detail
- **status** (string): The status of the projectstringAllowed values: approved archived rejected draft unlisted processing withheld scheduled private unknown approved
- **requested_status** (string): The requested status when submitting for review or scheduling the project for releasestring nullable Allowed values: approved archived unlisted private draft
- **additional_categories** (Array<string>): A list of categories which are searchable but non-primaryArray<string>[ "technology", "adventure", "fabric"]
- **issues_url** (string): An optional link to where to submit bugs or issues with the projectstring nullable https://github.com/my_user/my_project/issues
- **source_url** (string): An optional link to the source code of the projectstring nullable https://github.com/my_user/my_project
- **wiki_url** (string): An optional link to the project's wiki page or other relevant informationstring nullable https://github.com/my_user/my_project/wiki
- **discord_url** (string): An optional invite link to the project's discordstring nullable https://discord.gg/AaBbCcDd
- **donation_urls** (Array<object>): A list of donation links for the projectArray<object> object id The ID of the donation platformstringpatreon platform The donation platform this link is tostringPatreon url The URL of the donation pla
- **id** (string): The ID of the donation platformstringpatreon
- **platform** (string): The donation platform this link is tostringPatreon
- **url** (string): The URL of the donation platform and userstringhttps://www.patreon.com/my_user
- **license_id** (string): The SPDX license ID of a projectstringLGPL-3.0-or-later
- **license_url** (string): The URL to this licensestring nullable
- **moderation_message** (string): The title of the moderators' message for the projectstring nullable
- **moderation_message_body** (string): The body of the moderators' message for the projectstring nullable

## Responses

### 204

Expected response to a valid request

### 401

Incorrect token scopes or no authorization to access the requested item(s)

- **error** (string): The name of the errorstringunauthorized
- **description** (string): The contents of the errorstringAuthentication Error: Invalid Authentication Credentials

### 404

The requested item(s) were not found or no authorization to access the requested item(s)

---

## Projects (continued)

**Get multiple projects**

- Production server, Staging server

### Parameters

#### Query Parameters
- **ids** (string): string["AABBCCDD", "EEFFGGHH"]The IDs and/or slugs of the projects

### Responses
#### 200
Expected response to a valid request
- **slug** (string): The slug of a project, used for vanity URLs
- **title** (string): The title or name of the project
- **description** (string): A short description of the project
- **categories** (Array<string>): A list of the categories that the project has
- **client_side** (string): The client side support of the project
- **server_side** (string): The server side support of the project
- **body** (string): A long form description of the project
- **status** (string): The status of the project
- **requested_status** (string): The requested status when submitting for review
- **additional_categories** (Array<string>): A list of categories which are searchable but non-primary
- **issues_url** (string): An optional link to where to submit bugs or issues
- **source_url** (string): An optional link to the source code
- **wiki_url** (string): An optional link to the project's wiki page
- **discord_url** (string): An optional invite link to the project's discord
- **donation_urls** (Array<object>): A list of donation links for the project
- **project_type** (string): The project type
- **downloads** (integer): The total number of downloads
- **icon_url** (string): The URL of the project's icon
- **color** (integer): The RGB color of the project
- **thread_id** (string): The ID of the moderation thread
- **monetization_status** (string): The monetization status
- **id** (string): The ID of the project
- **team** (string): The ID of the team
- **moderator_message** (object): A message that a moderator sent
- **published** (string): The date the project was published
- **updated** (string): The date the project was last updated
- **followers** (integer): The total number of users following the project
- **license** (object): The license of the project
- **versions** (Array<string>): A list of the version IDs
- **game_versions** (Array<string>): A list of all of the game versions supported
- **loaders** (Array<string>): A list of all of the loaders supported
- **gallery** (Array<object>): A list of images in the project's gallery

---

**Bulk-edit multiple projects**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Query Parameters
- **ids** (string): The IDs and/or slugs of the projects

### Request Body
Fields to edit on all projects specified
- **categories** (Array<string>): Set all categories
- **add_categories** (Array<string>): Add categories
- **remove_categories** (Array<string>): Remove categories
- **additional_categories** (Array<string>): Set all additional categories
- **add_additional_categories** (Array<string>): Add additional categories
- **remove_additional_categories** (Array<string>): Remove additional categories
- **donation_urls** (Array<object>): Set all donation links
- **add_donation_urls** (Array<object>): Add donation links
- **remove_donation_urls** (Array<object>): Remove donation links
- **issues_url** (string): An optional link to submit bugs or issues
- **source_url** (string): An optional link to the source code
- **wiki_url** (string): An optional link to the wiki page
- **discord_url** (string): An optional invite link to the discord

### Responses
#### 204 Expected response to a valid request
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Get a list of random projects**

- Production server, Staging server

### Parameters
#### Query Parameters
- **count** (integer): <= 100, The number of random projects to return

### Responses
#### 200
Expected response to a valid request
Return fields same as Get a project response
#### 400 Request was invalid

---

**Create a project**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_CREATE

### Request Body
New project (multipart form data)
- **data** (object): Contains slug, title, description, categories, client_side, server_side, body, status, requested_status, additional_categories, issues_url, source_url, wiki_url, discord_url, donation_urls, license_id, license_url, project_type, initial_versions
- **icon** (string): Project icon file (binary, image formats)

### Responses
#### 200 Expected response to a valid request
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Delete project's icon**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Responses
#### 204 Expected response to a valid request
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Change project's icon**

- Production server, Staging server

The new icon may be up to 256KiB in size.

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

#### Query Parameters
- **ext** (string): Image extension (png, jpg, jpeg, bmp, gif, webp, svg, svgz, rgb)

### Responses
#### 204 Expected response to a valid request
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Check project slug/ID validity**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Responses
#### 200 Expected response to a valid request (returns project ID)
#### 404 Not found

---

**Add a gallery image**

- Production server, Staging server

Modrinth allows you to upload files of up to 5MiB to a project's gallery.

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project
#### Query Parameters
- **ext** (string): Image extension
- **featured** (boolean): Whether an image is featured
- **title** (string): Title of the image
- **description** (string): Description of the image
- **ordering** (integer): Ordering of the image

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes
#### 404 Not found

---

**Delete a gallery image**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project
#### Query Parameters
- **url** (string): URL link of the image to delete

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Modify a gallery image**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project
#### Query Parameters
- **url** (string): URL link of the image to modify
- **featured** (boolean): Whether the image is featured
- **title** (string): New title of the image
- **description** (string): New description of the image
- **ordering** (integer): New ordering of the image

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get all of a project's dependencies**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Responses
#### 200 Expected response to a valid request
Returns projects and versions that the project depends upon
#### 404 Not found

---

**Follow a project**

- Production server, Staging server

### Authorizations
- TokenAuth USER_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Unfollow a project**

- Production server, Staging server

### Authorizations
- TokenAuth USER_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Schedule a project**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Request Body
- **time** (string): ISO-8601 format date/time
- **requested_status** (string): The requested status when scheduling

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

## Versions

### Overview

Versions contain download links to files with additional metadata.

---

**List project's versions**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project
#### Query Parameters
- **loaders** (string): The types of loaders to filter for
- **game_versions** (string): The game versions to filter for
- **featured** (boolean): Filter for featured or non-featured versions
- **include_changelog** (boolean): Whether to include changelog field

### Responses
#### 200 Expected response to a valid request
Returns array of version objects
- **name** (string): The name of this version
- **version_number** (string): The version number
- **changelog** (string): The changelog for this version
- **dependencies** (Array<object>): A list of specific versions this version depends on
- **game_versions** (Array<string>): A list of supported Minecraft versions
- **version_type** (string): The release channel (release, beta, alpha)
- **loaders** (Array<string>): The mod loaders this version supports
- **featured** (boolean): Whether the version is featured
- **status** (string): The status (listed, archived, draft, unlisted, scheduled, unknown)
- **requested_status** (string): The requested status
- **id** (string): The version ID
- **project_id** (string): The project ID
- **author_id** (string): The author ID
- **date_published** (string): ISO-8601 date
- **downloads** (integer): Number of downloads
- **files** (Array<object>): A list of files available for download
#### 404 Not found

---

**Get a version**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id** (string): The ID of the version

### Responses
#### 200 Expected response to a valid request
Returns the full version object (same fields as List project's versions)
#### 404 Not found

---

**Delete a version**

- Production server, Staging server

### Authorizations
- TokenAuth VERSION_DELETE

### Parameters
#### Path Parameters
- **id** (string): The ID of the version

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Modify a version**

- Production server, Staging server

### Authorizations
- TokenAuth VERSION_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the version

### Request Body
Modified version fields
- **name** (string): The name of this version
- **version_number** (string): The version number
- **changelog** (string): The changelog
- **dependencies** (Array<object>): A list of dependencies
- **game_versions** (Array<string>): Supported Minecraft versions
- **version_type** (string): Release channel
- **loaders** (Array<string>): Mod loaders
- **featured** (boolean): Whether featured
- **status** (string): Status
- **requested_status** (string): Requested status
- **primary_file** (Array<string>): Hash format and hash of new primary file
- **file_types** (Array<object>): A list of file types to edit

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get a version given a version number or ID**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project
- **id|number** (string): The version ID or version number

### Responses
#### 200 Expected response to a valid request
#### 404 Not found

---

**Create a version**

- Production server, Staging server

Accepts .mrpack, .jar, .zip, and .litemod files. Multipart request with data and file fields.

### Authorizations
- TokenAuth VERSION_CREATE

### Request Body
- **data** (object): Version metadata (name, version_number, changelog, dependencies, game_versions, version_type, loaders, featured, status, requested_status, project_id, file_parts, primary_file)

### Responses
#### 200 Expected response (returns created version)
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Schedule a version**

- Production server, Staging server

### Authorizations
- TokenAuth VERSION_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the version

### Request Body
- **time** (string): ISO-8601 format date/time
- **requested_status** (string): The requested status

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Get multiple versions**

- Production server, Staging server

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the versions

### Responses
#### 200 Expected response to a valid request

---

**Add files to version**

- Production server, Staging server

### Authorizations
- TokenAuth VERSION_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the version

### Request Body
Multipart with file data

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

## Version-files

### Overview

Versions can contain multiple files, and these routes help manage those files.

---

**Get version from hash**

- Production server, Staging server

### Parameters
#### Path Parameters
- **hash** (string): The hash of the file in hexadecimal
#### Query Parameters
- **algorithm** (string): The algorithm (sha1 or sha512), default: sha1
- **multiple** (boolean): Whether to return multiple results

### Responses
#### 200 Expected response to a valid request
Returns the version object
#### 404 Not found

---

**Delete a file from its hash**

- Production server, Staging server

### Authorizations
- TokenAuth VERSION_WRITE

### Parameters
#### Path Parameters
- **hash** (string): The hash of the file
#### Query Parameters
- **algorithm** (string): The algorithm (sha1 or sha512), default: sha1
- **version_id** (string): Version ID to delete from (if multiple files share the hash)

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Latest version of a project from a hash, loader(s), and game version(s)**

- Production server, Staging server

### Parameters
#### Path Parameters
- **hash** (string): The hash of the file
#### Query Parameters
- **algorithm** (string): The algorithm, default: sha1

### Request Body
- **loaders** (Array<string>): Loaders to filter
- **game_versions** (Array<string>): Game versions to filter

### Responses
#### 200 Expected response (returns version)
#### 400 Request was invalid
#### 404 Not found

---

**Get versions from hashes**

- Production server, Staging server

Same as /version_file/{hash} except it accepts multiple hashes.

### Request Body
- **hashes** (Array<string>): Array of hashes
- **algorithm** (string): The algorithm used

### Responses
#### 200 Expected response (returns map from hashes to versions)
#### 400 Request was invalid

---

**Latest versions of multiple project from hashes, loader(s), and game version(s)**

- Production server, Staging server

Same as /version_file/{hash}/update except it accepts multiple hashes.

### Request Body
- **hashes** (Array<string>): Array of hashes
- **algorithm** (string): The algorithm
- **loaders** (Array<string>): Loaders
- **game_versions** (Array<string>): Game versions

### Responses
#### 200 Expected response (returns map from hashes to versions)
#### 400 Request was invalid

---

## Users

### Overview

Users can create projects, join teams, access notifications, manage settings, and follow projects. Admins and moderators have more advanced permissions such as reviewing new projects.

---

**Get a user**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 200 Expected response to a valid request
- **username** (string): The user's username
- **name** (string): The user's display name
- **email** (string): The user's email (only if requesting own account, requires USER_READ_EMAIL scope)
- **bio** (string): A description of the user
- **payout_data** (object): Payout status data (only your own)
- **id** (string): The user's ID
- **avatar_url** (string): The user's avatar URL
- **created** (string): When the user was created
- **role** (string): The user's role (admin, moderator, developer)
- **badges** (integer): Badges applicable to this user
- **auth_providers** (Array<string>): Authentication providers (only if requesting own account)
- **email_verified** (boolean): Whether email is verified
- **has_password** (boolean): Whether password is set
- **has_totp** (boolean): Whether TOTP 2FA is connected
#### 404 Not found

---

**Modify a user**

- Production server, Staging server

### Authorizations
- TokenAuth USER_WRITE

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Request Body
Modified user fields (username, name, email, bio, payout_data)

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get user from authorization header**

- Production server, Staging server

### Authorizations
- TokenAuth USER_READ

### Responses
#### 200 Expected response (returns the authenticated user)
#### 401 Incorrect token scopes

---

**Get multiple users**

- Production server, Staging server

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the users

### Responses
#### 200 Expected response to a valid request

---

**Remove user's avatar**

- Production server, Staging server

### Authorizations
- TokenAuth USER_WRITE

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 404 Not found

---

**Change user's avatar**

- Production server, Staging server

The new avatar may be up to 2MiB in size.

### Authorizations
- TokenAuth USER_WRITE

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 404 Not found

---

**Get user's projects**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 200 Returns array of projects
#### 404 Not found

---

**Get user's followed projects**

- Production server, Staging server

### Authorizations
- TokenAuth USER_READ

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 200 Returns array of projects
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get user's payout history**

- Production server, Staging server

### Authorizations
- TokenAuth PAYOUTS_READ

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 200 Expected response
- **all_time** (string): All-time balance in USD
- **last_month** (string): Amount made in previous 30 days
- **payouts** (Array<object>): History of past transactions
#### 401 Incorrect token scopes
#### 404 Not found

---

**Withdraw payout balance to PayPal or Venmo**

- Production server, Staging server

### Authorizations
- TokenAuth PAYOUTS_WRITE

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user
#### Query Parameters
- **amount** (integer): Amount to withdraw

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

## Notifications

### Overview

Notifications are sent to users for various reasons, including for project updates, team invites, and moderation purposes.

---

**Get user's notifications**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_READ

### Parameters
#### Path Parameters
- **id|username** (string): The ID or username of the user

### Responses
#### 200 Expected response
- **id** (string): The notification ID
- **user_id** (string): The user who received the notification
- **type** (string): The type (project_update, team_invite, status_change, moderator_message)
- **title** (string): The title
- **text** (string): The body text
- **link** (string): A link to the related project or version
- **read** (boolean): Whether read
- **created** (string): When created
- **actions** (Array<object>): A list of actions that can be performed
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get notification from ID**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_READ

### Parameters
#### Path Parameters
- **id** (string): The ID of the notification

### Responses
#### 200 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Delete notification**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the notification

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Mark notification as read**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the notification

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get multiple notifications**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_READ

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the notifications

### Responses
#### 200 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Delete multiple notifications**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_WRITE

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the notifications

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Mark multiple notifications as read**

- Production server, Staging server

### Authorizations
- TokenAuth NOTIFICATION_WRITE

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the notifications

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

## Threads

### Overview

Threads are a way of communicating between users and moderators, for the purposes of project reviews and reports.

---

**Get your open reports**

- Production server, Staging server

### Authorizations
- TokenAuth REPORT_READ

### Parameters
#### Query Parameters
- **count** (integer): Number of reports to return

### Responses
#### 200 Expected response
- **report_type** (string): The type of the report
- **item_id** (string): The ID of the item being reported
- **item_type** (string): The type (project, user, version)
- **body** (string): Extended explanation
- **id** (string): The report ID
- **reporter** (string): The reporter's user ID
- **created** (string): When created
- **closed** (boolean): Whether resolved
- **thread_id** (string): The moderation thread ID
#### 401 Incorrect token scopes
#### 404 Not found

---

**Report a project, user, or version**

- Production server, Staging server

### Authorizations
- TokenAuth REPORT_CREATE

### Request Body
- **report_type** (string): The type of report
- **item_id** (string): The ID of the item being reported
- **item_type** (string): The type (project, user, version)
- **body** (string): Extended explanation

### Responses
#### 200 Returns the created report
#### 400 Request was invalid
#### 401 Incorrect token scopes

---

**Get report from ID**

- Production server, Staging server

### Authorizations
- TokenAuth REPORT_READ

### Parameters
#### Path Parameters
- **id** (string): The ID of the report

### Responses
#### 200 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Modify a report**

- Production server, Staging server

### Authorizations
- TokenAuth REPORT_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the report

### Request Body
- **body** (string): The contents of the report
- **closed** (boolean): Whether the thread should be closed

### Responses
#### 204 Expected response
#### 400 Request was invalid
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get multiple reports**

- Production server, Staging server

### Authorizations
- TokenAuth REPORT_READ

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the reports

### Responses
#### 200 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get a thread**

- Production server, Staging server

### Authorizations
- TokenAuth THREAD_READ

### Parameters
#### Path Parameters
- **id** (string): The ID of the thread

### Responses
#### 200 Expected response
- **id** (string): Thread ID
- **type** (string): project, report, direct_message
- **project_id** (string): Associated project ID
- **report_id** (string): Associated report ID
- **messages** (Array<object>): Thread messages
- **members** (Array): Thread members (user objects)
#### 404 Not found

---

**Send a text message to a thread**

- Production server, Staging server

### Authorizations
- TokenAuth THREAD_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the thread

### Request Body
- **type** (string): Message type (status_change, text, thread_closure, deleted)
- **body** (string): The actual message text
- **private** (boolean): Whether only visible to moderators
- **replying_to** (string): The ID of the message being replied to
- **old_status** (string): Old status (for status_change type)
- **new_status** (string): New status (for status_change type)

### Responses
#### 200 Returns the updated thread
#### 400 Request was invalid
#### 404 Not found

---

**Get multiple threads**

- Production server, Staging server

### Authorizations
- TokenAuth THREAD_READ

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the threads

### Responses
#### 200 Expected response
#### 404 Not found

---

**Delete a thread message**

- Production server, Staging server

### Authorizations
- TokenAuth THREAD_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the message

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

## Teams

### Overview

Through teams, user permissions limit how team members can modify projects.

---

**Get a project's team members**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project

### Responses
#### 200 Returns an array of team members
- **team_id** (string): The team ID
- **user** (object): The user object with full details
- **role** (string): The user's role on the team
- **permissions** (integer): Permissions in bitfield format
- **accepted** (boolean): Whether the user accepted the invitation
- **payouts_split** (integer): Payout split proportion
- **ordering** (integer): Team member ordering
#### 404 Not found

---

**Get a team's members**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_READ

### Parameters
#### Path Parameters
- **id** (string): The ID of the team

### Responses
#### 200 Returns array of team members
#### 404 Not found

---

**Add a user to a team**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the team

### Request Body
- **user_id** (string): The ID of the user to add (usernames cannot be used here)

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Get the members of multiple teams**

- Production server, Staging server

### Parameters
#### Query Parameters
- **ids** (string): The IDs of the teams

### Responses
#### 200 Expected response

---

**Join a team**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the team

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Remove a member from a team**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the team
- **id|username** (string): The ID or username of the user

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Modify a team member's information**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the team
- **id|username** (string): The ID or username of the user

### Request Body
- **role** (string): The role (e.g. Contributor)
- **permissions** (integer): Permissions in bitfield format
- **payouts_split** (integer): Payout split proportion
- **ordering** (integer): Team member ordering

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

**Transfer team's ownership to another user**

- Production server, Staging server

### Authorizations
- TokenAuth PROJECT_WRITE

### Parameters
#### Path Parameters
- **id** (string): The ID of the team

### Request Body
- **user_id** (string): The new owner's ID

### Responses
#### 204 Expected response
#### 401 Incorrect token scopes
#### 404 Not found

---

## Tags

### Overview

Tags are common and reusable lists of metadata types such as categories or versions. Some can be applied to projects and/or versions.

---

**Get a list of categories**

- Production server, Staging server

Gets an array of categories, their icons, and applicable project types.

### Responses
#### 200 Expected response
- **icon** (string): The SVG icon of a category
- **name** (string): The name of the category
- **project_type** (string): The project type this category is applicable to
- **header** (string): The header under which the category should go

---

**Get a list of loaders**

- Production server, Staging server

Gets an array of loaders, their icons, and supported project types.

### Responses
#### 200 Expected response
- **icon** (string): The SVG icon of a loader
- **name** (string): The name of the loader
- **supported_project_types** (Array<string>): Project types applicable to this loader

---

**Get a list of game versions**

- Production server, Staging server

Gets an array of game versions and information about them.

### Responses
#### 200 Expected response
- **version** (string): The name/number of the game version
- **version_type** (string): The type (release, snapshot, alpha, beta)
- **date** (string): The date of the game version release
- **major** (boolean): Whether this is a major version

---

**Get a list of licenses**

- Production server, Staging server

Deprecated - simply use SPDX IDs.

### Responses
#### 200 Expected response
- **short** (string): The short identifier of the license
- **name** (string): The full name of the license

---

**Get the text and title of a license**

- Production server, Staging server

### Parameters
#### Path Parameters
- **id** (string): The license ID (e.g. LGPL-3.0-or-later)

### Responses
#### 200 Expected response
- **title** (string): License title
- **body** (string): Full license text
#### 400 Request was invalid

---

**Get a list of donation platforms**

- Production server, Staging server

### Responses
#### 200 Expected response
- **short** (string): The short identifier (e.g. bmac)
- **name** (string): The full name (e.g. Buy Me a Coffee)

---

**Get a list of report types**

- Production server, Staging server

### Responses
#### 200 Expected response
Returns array: ["spam", "copyright", "inappropriate", "malicious", "name-squatting", "other"]

---

**Get a list of project types**

- Production server, Staging server

### Responses
#### 200 Expected response
Returns array: ["mod", "modpack", "resourcepack", "shader"]

---

**Get a list of side types**

- Production server, Staging server

### Responses
#### 200 Expected response
Returns array: ["required", "optional", "unsupported", "unknown"]

---

## Misc

---

**Forge Updates JSON file**

- Production server, Staging server

If you're a Forge mod developer, your Modrinth mods have an automatically generated updates.json using the Forge Update Checker.

Insert the URL into the [[mods]] section of your mods.toml:

`
[[mods]]
updateJSONURL = "https://api.modrinth.com/updates/{slug|ID}/forge_updates.json"
`

### Parameters
#### Path Parameters
- **id|slug** (string): The ID or slug of the project
#### Query Parameters
- **neoforge** (string): Whether to include NeoForge versions (only, include, or omitted)

### Responses
#### 200 Expected response
- **homepage** (string): A link to the mod page
- **promos** (object): Recommended and latest versions for each Minecraft release
#### 400 Invalid request

---

**Various statistics about this Modrinth instance**

- Production server, Staging server

### Responses
#### 200 Expected response
- **projects** (integer): Number of projects on Modrinth
- **versions** (integer): Number of versions on Modrinth
- **files** (integer): Number of version files on Modrinth
- **authors** (integer): Number of authors (users with projects)
