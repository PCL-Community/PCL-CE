# CurseForge for Studios API Documentation

Source: https://docs.curseforge.com/rest-api/

---

## Getting Started

### An introduction to CurseForge

CurseForge is the home to your modding needs. Mods are in-game content that is created by independent creators, and adds a new layer of fun to games, often altering games so much that it creates brand new and exciting experiences within these games.

Any game that supports mods can be listed on CurseForge.

### What is CurseForge for Studios

CurseForge for Studios is a new way for game developers to bring their games to the forefront of modding. When logged in to the CurseForge for Studios console, game developers can set up their games to be available on the CurseForge website [www.curseforge.com](http://www.curseforge.com). Once on the CurseForge Website, authors can start uploading their creations for your game, and users playing your game can then download them through various methods.

> **Note:** Not a game developer? If you're running a 3rd party modding service, we have an API solution for you. Click [here](https://forms.monday.com/forms/dce5ccb7afda9a1c21dab1a1aa1d84eb?r=use1) to apply for a key.
>
> *Note: Any registered account which is not a game developer and did not request a key, will be deleted*

### What can CurseForge for Studios do? What can you do with it?

CurseForge for Studios games are supported on multiple platforms, from PC, to consoles and more.

As a game developer, you can decide if you want your game users to download mods through various methods, such as a frame within your game launcher (if you have one), an in-game UI built using our advanced API options, plugins for different consoles, such as XBOX, Playstation, etc. In addition, you can make your game's mods available publicly on the CurseForge website, the CurseForge client, and other 3rd party mod managers out there if you choose so.

Last but not least, you can choose to let us moderate the content created for your game, or moderate it yourself with your own moderation team.

### Supported download options

- Cross-Platform plugins, SDKs
- In-Game UI, using CurseForge's API
- Game-Launcher UI that you build, using CurseForge's API
- Game-Launcher UI, using a pre-built customizable frame
- CurseForge Website
- CurseForge Client
- 3rd Party mod managers

### Your next steps

1. **Create an account on CurseForge for Studios Console** — Go to the [CurseForge for Studios Create account page](https://console.curseforge.com/?#/signup), and create your account.
2. **Set up your game** — Hit the "Add a game" button and follow the process.
3. **Test your game** — Once your game is set up, start testing it to make sure everything works.
4. **Publish** — When done testing, send your game for approval. Once approved, you can publish whenever you're ready.
5. **Talk to us for any help** — [Contact us](https://support.curseforge.com/en/support/solutions/articles/9000205544-contact-us).

### Setting up a game, Step by step guide

1. Login, or create an account on CurseForge for Studios
2. Set up your organization account, or skip
3. Once on the home page, click the "Add a game" button
4. Fill in the required details in the following setup screens (Game assets, Game versions, Categories & Projects)
5. Choose the users you want to test it in the "Testers tab"
6. When done testing, visit the "Status" tab and submit your game for review
7. Once approved, go ahead and publish whenever you are ready
8. Your game is now published and available on the CurseForge website and the API

---

## Accessing the service

### Base URL

All endpoints use the same base URL — `https://api.curseforge.com`

### Pagination Limits

The maximum page size is 50 results per page and capped at 10000 total results.

> **Note:** The limit is (index + pageSize <= 10,000).

### Notes

- Unless stated otherwise all int32 responses are unsigned.

### Authentication

- **API Key (API_KEY)** — Parameter Name: `x-api-key`, in: header. The API key can be generated in the CurseForge for Studios [developer console](https://console.curseforge.com/).

---

## Games

### Get Games

```
GET /v1/games
```

```shell
curl -X GET /v1/games \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all games that are available to the provided API key.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| index | query | integer(int32) | false | A zero based index of the first item to include in the response, the limit is: (index + pageSize <= 10,000). |
| pageSize | query | integer(int32) | false | The number of items to include in the response, the default/maximum value is 50. |

**Example Response (200)**

```json
{
  "data": [
    {
      "id": 0,
      "name": "string",
      "slug": "string",
      "dateModified": "2019-08-24T14:15:22Z",
      "assets": {
        "iconUrl": "string",
        "tileUrl": "string",
        "coverUrl": "string"
      },
      "status": 1,
      "apiStatus": 1
    }
  ],
  "pagination": {
    "index": 0,
    "pageSize": 0,
    "resultCount": 0,
    "totalCount": 0
  }
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Games Response |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Game

```
GET /v1/games/{gameId}
```

```shell
curl -X GET /v1/games/{gameId} \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a single game. A private game is only accessible by its respective API key.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | path | integer(int32) | true | A game unique id |

**Example Response (200)**

```json
{
  "data": {
    "id": 0,
    "name": "string",
    "slug": "string",
    "dateModified": "2019-08-24T14:15:22Z",
    "assets": {
      "iconUrl": "string",
      "tileUrl": "string",
      "coverUrl": "string"
    },
    "status": 1,
    "apiStatus": 1
  }
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Game Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Versions

```
GET /v1/games/{gameId}/versions
```

```shell
curl -X GET /v1/games/{gameId}/versions \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all available versions for each known version type of the specified game. A private game is only accessible to its respective API key.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | path | integer(int32) | true | A game unique id |

**Example Response (200)**

```json
{
  "data": [
    {
      "type": 0,
      "versions": [ "string" ]
    }
  ]
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Versions Response - V1 |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Version Types

```
GET /v1/games/{gameId}/version-types
```

```shell
curl -X GET /v1/games/{gameId}/version-types \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all available version types of the specified game. A private game is only accessible to its respective API key.

Currently, when creating games via the CurseForge for Studios Console, you are limited to a single game version type.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | path | integer(int32) | true | A game unique id |

**Example Response (200)**

```json
{
  "data": [
    {
      "id": 0,
      "gameId": 0,
      "name": "string",
      "slug": "string",
      "isSyncable": true,
      "status": 1
    }
  ]
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Version Types Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Versions - V2

```
GET /v2/games/{gameId}/versions
```

```shell
curl -X GET /v2/games/{gameId}/versions \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all available versions for each known version type of the specified game. A private game is only accessible to its respective API key.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | path | integer(int32) | true | A game unique id |

**Example Response (200)**

```json
{
  "data": [
    {
      "type": 0,
      "versions": [
        {
          "id": 0,
          "slug": "string",
          "name": "string"
        }
      ]
    }
  ]
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Versions Response - V2 |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

## Categories

### Get Categories

```
GET /v1/categories
```

```shell
curl -X GET /v1/categories?gameId=0 \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all available classes and categories of the specified game. Specify a game id for a list of all game categories, or a class id for a list of categories under that class. Specify the classesOnly flag to just get the classes for a given game.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | query | integer(int32) | true | A game unique id |
| classId | query | integer(int32) | false | A class unique id |
| classesOnly | query | boolean | false | A flag used with gameId to return only classes |

**Example Response (200)**

```json
{
  "data": [
    {
      "id": 0,
      "gameId": 0,
      "name": "string",
      "slug": "string",
      "url": "string",
      "iconUrl": "string",
      "dateModified": "2019-08-24T14:15:22Z",
      "isClass": true,
      "classId": 0,
      "parentCategoryId": 0,
      "displayIndex": 0
    }
  ]
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Categories Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

## Mods

### Search Mods

```
GET /v1/mods/search
```

```shell
curl -X GET /v1/mods/search?gameId=0 \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all mods that match the search criteria.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | query | integer(int32) | true | Filter by game id |
| classId | query | integer(int32) | false | Filter by section id (discoverable via Categories) |
| categoryId | query | integer(int32) | false | Filter by category id |
| categoryIds | query | string | false | Filter by a list of category ids - this will override categoryId |
| gameVersion | query | string | false | Filter by game version string |
| gameVersions | query | string | false | Filter by a list of game version strings - this will override gameVersion |
| searchFilter | query | string | false | Filter by free text search in the mod name and author |
| sortField | query | ModsSearchSortField | false | Filter by ModsSearchSortField enumeration |
| sortOrder | query | SortOrder | false | 'asc' if sort is in ascending order, 'desc' if sort is in descending order |
| modLoaderType | query | ModLoaderType | false | Filter only mods associated to a given modloader (Forge, Fabric ...). Must be coupled with gameVersion. |
| modLoaderTypes | query | string | false | Filter by a list of mod loader types - this will override modLoaderType |
| gameVersionTypeId | query | integer(int32) | false | Filter only mods that contain files tagged with versions of the given gameVersionTypeId |
| authorId | query | integer(int32) | false | Filter only mods that the given authorId is a member of |
| primaryAuthorId | query | integer(int32) | false | Filter only mods that the given primaryAuthorId is the owner of |
| slug | query | string | false | Filter by slug (coupled with classId will result in a unique result) |
| index | query | integer(int32) | false | A zero based index of the first item to include in the response, the limit is: (index + pageSize <= 10,000). |
| pageSize | query | integer(int32) | false | The number of items to include in the response, the default/maximum value is 50 |

**Detailed descriptions**

- **categoryIds**: Filter by a list of category ids — this will override categoryId. Format: `categoryIds=[1,2,3...]`. Max 10 category ids per query.
- **gameVersions**: Filter by a list of game version strings — this will override gameVersion. Format: `gameVersions=["1.19.1", "1.19.2"...]`. Max 4 per query.
- **modLoaderTypes**: Filter by a list of mod loader types — this will override modLoaderType. Format: `modLoaderTypes=[Forge, Fabric, ...]`. Max 5 values.

**Enumerated Values**

| Parameter | Value |
|-----------|-------|
| sortField | 1=Featured, 2=Popularity, 3=LastUpdated, 4=Name, 5=Author, 6=TotalDownloads, 7=Category, 8=GameVersion, 9=EarlyAccess, 10=FeaturedReleased, 11=ReleasedDate, 12=Rating |
| sortOrder | asc, desc |
| modLoaderType | 0=Any, 1=Forge, 2=Cauldron, 3=LiteLoader, 4=Fabric, 5=Quilt, 6=NeoForge |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Search Mods Response |
| 400 | Bad Request | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Mod

```
GET /v1/mods/{modId}
```

```shell
curl -X GET /v1/mods/{modId} \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a single mod.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modId | path | integer(int32) | true | The mod id |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Mod Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Mods

```
POST /v1/mods
```

```shell
curl -X POST /v1/mods \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a list of mods belonging to the same game.

**Body parameter**

```json
{
  "modIds": [ 0 ],
  "filterPcOnly": true
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| body | body | GetModsByIdsListRequestBody | true | Request body containing an array of mod ids, mod ids must belong to the same game |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Mods Response |
| 400 | Bad Request | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Featured Mods

```
POST /v1/mods/featured
```

```shell
curl -X POST /v1/mods/featured \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a list of featured, popular and recently updated mods.

**Body parameter**

```json
{
  "gameId": 0,
  "excludedModIds": [ 0 ],
  "gameVersionTypeId": 0
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| body | body | GetFeaturedModsRequestBody | true | Match results for a game and exclude specific mods |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Featured Mods Response |
| 400 | Bad Request | none | None |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Mod Description

```
GET /v1/mods/{modId}/description
```

```shell
curl -X GET /v1/mods/{modId}/description \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get the full description of a mod in HTML format.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modId | path | integer(int32) | true | The mod id |
| raw | query | boolean | false | none |
| stripped | query | boolean | false | none |
| markup | query | boolean | false | none |

**Example Response (200)**

```json
{
  "data": "string"
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | String Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

## Files

### Get Mod File

```
GET /v1/mods/{modId}/files/{fileId}
```

```shell
curl -X GET /v1/mods/{modId}/files/{fileId} \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a single file of the specified mod.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modId | path | integer(int32) | true | The mod id the file belongs to |
| fileId | path | integer(int32) | true | The file id |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Mod File Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Mod Files

```
GET /v1/mods/{modId}/files
```

```shell
curl -X GET /v1/mods/{modId}/files \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get all files of the specified mod.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modId | path | integer(int32) | true | The mod id the files belong to |
| gameVersion | query | string | false | Filter by game version string |
| modLoaderType | query | ModLoaderType | false | ModLoaderType enumeration |
| gameVersionTypeId | query | integer(int32) | false | Filter only files that are tagged with versions of the given gameVersionTypeId |
| index | query | integer(int32) | false | A zero based index of the first item to include in the response |
| pageSize | query | integer(int32) | false | The number of items to include in the response, the default/maximum value is 50 |

**Detailed descriptions**

- **modLoaderType**: ModLoaderType enumeration. Filter only files associated to a given modloader (Forge, Fabric ...).

**Enumerated Values**

| Parameter | Value |
|-----------|-------|
| modLoaderType | 0=Any, 1=Forge, 2=Cauldron, 3=LiteLoader, 4=Fabric, 5=Quilt, 6=NeoForge |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Mod Files Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Files

```
POST /v1/mods/files
```

```shell
curl -X POST /v1/mods/files \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a list of files.

**Body parameter**

```json
{
  "fileIds": [ 0 ]
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| body | body | GetModFilesRequestBody | true | Request body containing a list of file ids to fetch |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Files Response |
| 400 | Bad Request | none | None |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Mod File Changelog

```
GET /v1/mods/{modId}/files/{fileId}/changelog
```

```shell
curl -X GET /v1/mods/{modId}/files/{fileId}/changelog \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get the changelog of a file in HTML format.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modId | path | integer(int32) | true | The mod id the file belongs to |
| fileId | path | integer(int32) | true | The file id |

**Example Response (200)**

```json
{
  "data": "string"
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | String Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Mod File Download URL

```
GET /v1/mods/{modId}/files/{fileId}/download-url
```

```shell
curl -X GET /v1/mods/{modId}/files/{fileId}/download-url \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get a download url for a specific file.

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modId | path | integer(int32) | true | The mod id the file belongs to |
| fileId | path | integer(int32) | true | The file id |

**Example Response (200)**

```json
{
  "data": "string"
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | String Response |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

## Fingerprints

### Get Fingerprints Matches By Game Id

```
POST /v1/fingerprints/{gameId}
```

```shell
curl -X POST /v1/fingerprints/{gameId} \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get mod files that match a list of fingerprints for a given game id.

**Body parameter**

```json
{
  "fingerprints": [ 0 ]
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | path | integer(int32) | true | The game id for matching fingerprints |
| body | body | GetFingerprintMatchesRequestBody | true | The request body containing an array of fingerprints |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Fingerprint Matches Response |
| 400 | Bad Request | none | None |
| 503 | Service Unavailable | none | string |

*Authentication: API_KEY*

---

### Get Fingerprints Matches

```
POST /v1/fingerprints
```

```shell
curl -X POST /v1/fingerprints \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get mod files that match a list of fingerprints.

**Body parameter**

```json
{
  "fingerprints": [ 0 ]
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| body | body | GetFingerprintMatchesRequestBody | true | The request body containing an array of fingerprints |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Fingerprint Matches Response |
| 400 | Bad Request | none | None |
| 503 | Service Unavailable | none | string |

*Authentication: API_KEY*

---

### Get Fingerprints Fuzzy Matches By Game Id

```
POST /v1/fingerprints/fuzzy/{gameId}
```

```shell
curl -X POST /v1/fingerprints/fuzzy/{gameId} \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get mod files that match a list of fingerprints using fuzzy matching.

**Body parameter**

```json
{
  "gameId": 0,
  "fingerprints": [
    {
      "foldername": "string",
      "fingerprints": [ 0 ]
    }
  ]
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameId | path | integer(int32) | true | The game id for matching fingerprints |
| body | body | GetFuzzyMatchesRequestBody | true | Game id and folder fingerprints options for the fuzzy matching |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Fingerprints Fuzzy Matches Response |
| 400 | Bad Request | none | None |
| 503 | Service Unavailable | none | string |

*Authentication: API_KEY*

---

### Get Fingerprints Fuzzy Matches

```
POST /v1/fingerprints/fuzzy
```

```shell
curl -X POST /v1/fingerprints/fuzzy \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

Get mod files that match a list of fingerprints using fuzzy matching.

**Body parameter**

```json
{
  "gameId": 0,
  "fingerprints": [
    {
      "foldername": "string",
      "fingerprints": [ 0 ]
    }
  ]
}
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| body | body | GetFuzzyMatchesRequestBody | true | Game id and folder fingerprints options for the fuzzy matching |

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | Get Fingerprints Fuzzy Matches Response |
| 400 | Bad Request | none | None |
| 503 | Service Unavailable | none | string |

*Authentication: API_KEY*

---

## Minecraft

### Get Minecraft Versions

```
GET /v1/minecraft/version
```

```shell
curl -X GET /v1/minecraft/version \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| sortDescending | query | boolean | false | none |

**Example Response (200)**

```json
{
  "data": [
    {
      "id": 0,
      "gameVersionId": 0,
      "versionString": "string",
      "jarDownloadUrl": "string",
      "jsonDownloadUrl": "string",
      "approved": true,
      "dateModified": "2019-08-24T14:15:22Z",
      "gameVersionTypeId": 0,
      "gameVersionStatus": 1,
      "gameVersionTypeStatus": 1
    }
  ]
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | ApiResponseOfListOfMinecraftGameVersion |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Specific Minecraft Version

```
GET /v1/minecraft/version/{gameVersionString}
```

```shell
curl -X GET /v1/minecraft/version/{gameVersionString} \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| gameVersionString | path | string | true | none |

**Example Response (200)**

```json
{
  "data": {
    "id": 0,
    "gameVersionId": 0,
    "versionString": "string",
    "jarDownloadUrl": "string",
    "jsonDownloadUrl": "string",
    "approved": true,
    "dateModified": "2019-08-24T14:15:22Z",
    "gameVersionTypeId": 0,
    "gameVersionStatus": 1,
    "gameVersionTypeStatus": 1
  }
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | ApiResponseOfMinecraftGameVersion |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Minecraft ModLoaders

```
GET /v1/minecraft/modloader
```

```shell
curl -X GET /v1/minecraft/modloader \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| version | query | string | false | none |
| includeAll | query | boolean | false | none |

**Example Response (200)**

```json
{
  "data": [
    {
      "name": "string",
      "gameVersion": "string",
      "latest": true,
      "recommended": true,
      "dateModified": "2019-08-24T14:15:22Z",
      "type": 0
    }
  ]
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | ApiResponseOfListOfMinecraftModLoaderIndex |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

### Get Specific Minecraft ModLoader

```
GET /v1/minecraft/modloader/{modLoaderName}
```

```shell
curl -X GET /v1/minecraft/modloader/{modLoaderName} \
  -H 'Accept: application/json' \
  -H 'x-api-key: API_KEY'
```

**Parameters**

| Name | In | Type | Required | Description |
|------|----|------|----------|-------------|
| modLoaderName | path | string | true | none |

**Example Response (200)**

```json
{
  "data": {
    "id": 0,
    "gameVersionId": 0,
    "minecraftGameVersionId": 0,
    "forgeVersion": "string",
    "name": "string",
    "type": 0,
    "downloadUrl": "string",
    "filename": "string",
    "installMethod": 1,
    "latest": true,
    "recommended": true,
    "approved": true,
    "dateModified": "2019-08-24T14:15:22Z",
    "mavenVersionString": "string",
    "versionJson": "string",
    "librariesInstallLocation": "string",
    "minecraftVersion": "string",
    "additionalFilesJson": "string",
    "modLoaderGameVersionId": 0,
    "modLoaderGameVersionTypeId": 0,
    "modLoaderGameVersionStatus": 1,
    "modLoaderGameVersionTypeStatus": 1,
    "mcGameVersionId": 0,
    "mcGameVersionTypeId": 0,
    "mcGameVersionStatus": 1,
    "mcGameVersionTypeStatus": 1,
    "installProfileJson": "string"
  }
}
```

**Responses**

| Status | Meaning | Description | Schema |
|--------|---------|-------------|--------|
| 200 | OK | none | ApiResponseOfMinecraftModLoaderVersion |
| 404 | Not Found | none | None |
| 500 | Internal Server Error | none | None |

*Authentication: API_KEY*

---

## Schemas

### ApiResponseOfListOfMinecraftGameVersion

```json
{
  "data": [
    {
      "id": 0,
      "gameVersionId": 0,
      "versionString": "string",
      "jarDownloadUrl": "string",
      "jsonDownloadUrl": "string",
      "approved": true,
      "dateModified": "2019-08-24T14:15:22Z",
      "gameVersionTypeId": 0,
      "gameVersionStatus": 1,
      "gameVersionTypeStatus": 1
    }
  ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [MinecraftGameVersion] | The response data |

---

### ApiResponseOfListOfMinecraftModLoaderIndex

```json
{
  "data": [
    {
      "name": "string",
      "gameVersion": "string",
      "latest": true,
      "recommended": true,
      "dateModified": "2019-08-24T14:15:22Z",
      "type": 0
    }
  ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [MinecraftModLoaderIndex] | The response data |

---

### ApiResponseOfMinecraftGameVersion

```json
{
  "data": {
    "id": 0,
    "gameVersionId": 0,
    "versionString": "string",
    "jarDownloadUrl": "string",
    "jsonDownloadUrl": "string",
    "approved": true,
    "dateModified": "2019-08-24T14:15:22Z",
    "gameVersionTypeId": 0,
    "gameVersionStatus": 1,
    "gameVersionTypeStatus": 1
  }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | MinecraftGameVersion | The response data |

---

### ApiResponseOfMinecraftModLoaderVersion

```json
{
  "data": {
    "id": 0,
    "gameVersionId": 0,
    "minecraftGameVersionId": 0,
    "forgeVersion": "string",
    "name": "string",
    "type": 0,
    "downloadUrl": "string",
    "filename": "string",
    "installMethod": 1,
    "latest": true,
    "recommended": true,
    "approved": true,
    "dateModified": "2019-08-24T14:15:22Z",
    "mavenVersionString": "string",
    "versionJson": "string",
    "librariesInstallLocation": "string",
    "minecraftVersion": "string",
    "additionalFilesJson": "string",
    "modLoaderGameVersionId": 0,
    "modLoaderGameVersionTypeId": 0,
    "modLoaderGameVersionStatus": 1,
    "modLoaderGameVersionTypeStatus": 1,
    "mcGameVersionId": 0,
    "mcGameVersionTypeId": 0,
    "mcGameVersionStatus": 1,
    "mcGameVersionTypeStatus": 1,
    "installProfileJson": "string"
  }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | MinecraftModLoaderVersion | The response data |

---

### Category

```json
{
  "id": 0,
  "gameId": 0,
  "name": "string",
  "slug": "string",
  "url": "string",
  "iconUrl": "string",
  "dateModified": "2019-08-24T14:15:22Z",
  "isClass": true,
  "classId": 0,
  "parentCategoryId": 0,
  "displayIndex": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | The category id |
| gameId | integer(int32) | The game id related to the category |
| name | string | Category name |
| slug | string | The category slug as it appears in the URL |
| url | string | The category URL |
| iconUrl | string | URL for the category icon |
| dateModified | string(date-time) | Last modified date of the category |
| isClass | boolean\|null | A top level category for other categories |
| classId | integer(int32)\|null | The class id of the category |
| parentCategoryId | integer(int32)\|null | The parent category for this category |
| displayIndex | integer(int32)\|null | The display index for this category |

---

### CoreApiStatus

```
1
```

Possible values: 1=Private, 2=Public

---

### CoreStatus

```
1
```

Possible values: 1=Draft, 2=Test, 3=PendingReview, 4=Rejected, 5=Approved, 6=Live

---

### FeaturedModsResponse

```json
{
  "featured": [ { "...mod..." } ],
  "popular": [ { "...mod..." } ],
  "recentlyUpdated": [ { "...mod..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| featured | [Mod] | none |
| popular | [Mod] | none |
| recentlyUpdated | [Mod] | none |

---

### File

```json
{
  "id": 0,
  "gameId": 0,
  "modId": 0,
  "isAvailable": true,
  "displayName": "string",
  "fileName": "string",
  "releaseType": 1,
  "fileStatus": 1,
  "hashes": [
    { "value": "string", "algo": 1 }
  ],
  "fileDate": "2019-08-24T14:15:22Z",
  "fileLength": 0,
  "downloadCount": 0,
  "fileSizeOnDisk": 0,
  "downloadUrl": "string",
  "gameVersions": [ "string" ],
  "sortableGameVersions": [
    {
      "gameVersionName": "string",
      "gameVersionPadded": "string",
      "gameVersion": "string",
      "gameVersionReleaseDate": "2019-08-24T14:15:22Z",
      "gameVersionTypeId": 0
    }
  ],
  "dependencies": [
    { "modId": 0, "relationType": 1 }
  ],
  "exposeAsAlternative": true,
  "parentProjectFileId": 0,
  "alternateFileId": 0,
  "isServerPack": true,
  "serverPackFileId": 0,
  "isEarlyAccessContent": true,
  "earlyAccessEndDate": "2019-08-24T14:15:22Z",
  "fileFingerprint": 0,
  "modules": [
    { "name": "string", "fingerprint": 0 }
  ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | The file id |
| gameId | integer(int32) | The game id related to the mod that this file belongs to |
| modId | integer(int32) | The mod id |
| isAvailable | boolean | Whether the file is available to download |
| displayName | string | Display name of the file |
| fileName | string | Exact file name |
| releaseType | FileReleaseType | The file release type |
| fileStatus | FileStatus | Status of the file |
| hashes | [FileHash] | The file hash (i.e. md5 or sha1) |
| fileDate | string(date-time) | The file timestamp |
| fileLength | integer(int64) | The file length in bytes |
| downloadCount | integer(int64) | The number of downloads for the file |
| fileSizeOnDisk | integer(int64)\|null | The file's size on disk |
| downloadUrl | string | The file download URL |
| gameVersions | [string] | List of game versions this file is relevant for |
| sortableGameVersions | [SortableGameVersion] | Metadata used for sorting by game versions |
| dependencies | [FileDependency] | List of dependencies files |
| exposeAsAlternative | boolean\|null | none |
| parentProjectFileId | integer(int32)\|null | none |
| alternateFileId | integer(int32)\|null | none |
| isServerPack | boolean\|null | none |
| serverPackFileId | integer(int32)\|null | none |
| isEarlyAccessContent | boolean\|null | none |
| earlyAccessEndDate | string(date-time)\|null | none |
| fileFingerprint | integer(int64) | none |
| modules | [FileModule] | none |

---

### FileDependency

```json
{
  "modId": 0,
  "relationType": 1
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| modId | integer(int32) | none |
| relationType | FileRelationType | 1=EmbeddedLibrary, 2=OptionalDependency, 3=RequiredDependency, 4=Tool, 5=Incompatible, 6=Include |

---

### FileHash

```json
{
  "value": "string",
  "algo": 1
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| value | string | none |
| algo | HashAlgo | 1=Sha1, 2=Md5 |

---

### FileIndex

```json
{
  "gameVersion": "string",
  "fileId": 0,
  "filename": "string",
  "releaseType": 1,
  "gameVersionTypeId": 0,
  "modLoader": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| gameVersion | string | none |
| fileId | integer(int32) | none |
| filename | string | none |
| releaseType | FileReleaseType | 1=Release, 2=Beta, 3=Alpha |
| gameVersionTypeId | integer(int32)\|null | none |
| modLoader | ModLoaderType | 0=Any, 1=Forge, 2=Cauldron, 3=LiteLoader, 4=Fabric, 5=Quilt, 6=NeoForge |

---

### FileModule

```json
{
  "name": "string",
  "fingerprint": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| name | string | none |
| fingerprint | integer(int64) | none |

---

### FileRelationType

```
1
```

Possible values: 1=EmbeddedLibrary, 2=OptionalDependency, 3=RequiredDependency, 4=Tool, 5=Incompatible, 6=Include

---

### FileReleaseType

```
1
```

Possible values: 1=Release, 2=Beta, 3=Alpha

---

### FileStatus

```
1
```

Possible values: 1=Processing, 2=ChangesRequired, 3=UnderReview, 4=Approved, 5=Rejected, 6=MalwareDetected, 7=Deleted, 8=Archived, 9=Testing, 10=Released, 11=ReadyForReview, 12=Deprecated, 13=Baking, 14=AwaitingPublishing, 15=FailedPublishing, 16=Cooking, 17=Cooked, 18=UnderManualReview, 19=ScanningForMalware, 20=ProcessingFile, 21=PendingRelease, 22=ReadyForCooking, 23=PostProcessing

---

### FingerprintFuzzyMatch

```json
{
  "id": 0,
  "file": { "...File..." },
  "latestFiles": [ { "...File..." } ],
  "fingerprints": [ 0 ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| file | File | none |
| latestFiles | [File] | none |
| fingerprints | [integer] | none |

---

### FingerprintFuzzyMatchResult

```json
{
  "fuzzyMatches": [ { "...FingerprintFuzzyMatch..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| fuzzyMatches | [FingerprintFuzzyMatch] | none |

---

### FingerprintMatch

```json
{
  "id": 0,
  "file": { "...File..." },
  "latestFiles": [ { "...File..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| file | File | none |
| latestFiles | [File] | none |

---

### FingerprintsMatchesResult

```json
{
  "isCacheBuilt": true,
  "exactMatches": [ { "...FingerprintMatch..." } ],
  "exactFingerprints": [ 0 ],
  "partialMatches": [ { "...FingerprintMatch..." } ],
  "partialMatchFingerprints": { },
  "installedFingerprints": [ 0 ],
  "unmatchedFingerprints": [ 0 ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| isCacheBuilt | boolean | none |
| exactMatches | [FingerprintMatch] | none |
| exactFingerprints | [integer] | none |
| partialMatches | [FingerprintMatch] | none |
| partialMatchFingerprints | object | none |
| installedFingerprints | [integer] | none |
| unmatchedFingerprints | [integer] | none |

---

### FolderFingerprint

```json
{
  "foldername": "string",
  "fingerprints": [ 0 ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| foldername | string | none |
| fingerprints | [integer] | none |

---

### Game

```json
{
  "id": 0,
  "name": "string",
  "slug": "string",
  "dateModified": "2019-08-24T14:15:22Z",
  "assets": {
    "iconUrl": "string",
    "tileUrl": "string",
    "coverUrl": "string"
  },
  "status": 1,
  "apiStatus": 1
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| name | string | none |
| slug | string | none |
| dateModified | string(date-time) | none |
| assets | GameAssets | none |
| status | CoreStatus | 1=Draft, 2=Test, 3=PendingReview, 4=Rejected, 5=Approved, 6=Live |
| apiStatus | CoreApiStatus | 1=Private, 2=Public |

---

### GameAssets

```json
{
  "iconUrl": "string",
  "tileUrl": "string",
  "coverUrl": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| iconUrl | string | none |
| tileUrl | string | none |
| coverUrl | string | none |

---

### GameVersion

```json
{
  "id": 0,
  "slug": "string",
  "name": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| slug | string | none |
| name | string | none |

---

### GameVersionsByType

```json
{
  "type": 0,
  "versions": [ "string" ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| type | integer(int32) | none |
| versions | [string] | none |

---

### GameVersionsByType2

```json
{
  "type": 0,
  "versions": [
    { "id": 0, "slug": "string", "name": "string" }
  ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| type | integer(int32) | none |
| versions | [GameVersion] | none |

---

### GameVersionStatus

```
1
```

Possible values: 1=Approved, 2=Deleted, 3=New

---

### GameVersionType

```json
{
  "id": 0,
  "gameId": 0,
  "name": "string",
  "slug": "string",
  "isSyncable": true,
  "status": 1
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| gameId | integer(int32) | none |
| name | string | none |
| slug | string | none |
| isSyncable | boolean | none |
| status | GameVersionTypeStatus | 1=Normal, 2=Deleted |

---

### GameVersionTypeStatus

```
1
```

Possible values: 1=Normal, 2=Deleted

---

### Get Categories Response

```json
{
  "data": [ { "...Category..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [Category] | The response data |

---

### Get Featured Mods Response

```json
{
  "data": { "...FeaturedModsResponse..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | FeaturedModsResponse | The response data |

---

### Get Files Response

```json
{
  "data": [ { "...File..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [File] | The response data |

---

### Get Fingerprint Matches Response

```json
{
  "data": { "...FingerprintsMatchesResult..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | FingerprintsMatchesResult | The response data |

---

### Get Fingerprints Fuzzy Matches Response

```json
{
  "data": { "...FingerprintFuzzyMatchResult..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | FingerprintFuzzyMatchResult | The response data |

---

### Get Game Response

```json
{
  "data": { "...Game..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | Game | The response data |

---

### Get Games Response

```json
{
  "data": [ { "...Game..." } ],
  "pagination": { "...Pagination..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [Game] | The response data |
| pagination | Pagination | The response pagination information |

---

### Get Mod File Response

```json
{
  "data": { "...File..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | File | The response data |

---

### Get Mod Files Response

```json
{
  "data": [ { "...File..." } ],
  "pagination": { "...Pagination..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [File] | The response data |
| pagination | Pagination | The response pagination information |

---

### Get Mod Response

```json
{
  "data": { "...Mod..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | Mod | The response data |

---

### Get Mods Response

```json
{
  "data": [ { "...Mod..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [Mod] | The response data |

---

### Get Version Types Response

```json
{
  "data": [ { "...GameVersionType..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [GameVersionType] | The response data |

---

### Get Versions Response - V1

```json
{
  "data": [ { "...GameVersionsByType..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [GameVersionsByType] | The response data |

---

### Get Versions Response - V2

```json
{
  "data": [ { "...GameVersionsByType2..." } ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [GameVersionsByType2] | The response data |

---

### GetFeaturedModsRequestBody

```json
{
  "gameId": 0,
  "excludedModIds": [ 0 ],
  "gameVersionTypeId": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| gameId | integer(int32) | none |
| excludedModIds | [integer] | none |
| gameVersionTypeId | integer(int32)\|null | none |

---

### GetFingerprintMatchesRequestBody

```json
{
  "fingerprints": [ 0 ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| fingerprints | [integer] | none |

---

### GetFuzzyMatchesRequestBody

```json
{
  "gameId": 0,
  "fingerprints": [
    { "foldername": "string", "fingerprints": [ 0 ] }
  ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| gameId | integer(int32) | none |
| fingerprints | [FolderFingerprint] | none |

---

### GetModFilesRequestBody

```json
{
  "fileIds": [ 0 ]
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| fileIds | [integer] | none |

---

### GetModsByIdsListRequestBody

```json
{
  "modIds": [ 0 ],
  "filterPcOnly": true
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| modIds | [integer] | none |
| filterPcOnly | boolean\|null | none |

---

### HashAlgo

```
1
```

Possible values: 1=Sha1, 2=Md5

---

### MinecraftGameVersion

```json
{
  "id": 0,
  "gameVersionId": 0,
  "versionString": "string",
  "jarDownloadUrl": "string",
  "jsonDownloadUrl": "string",
  "approved": true,
  "dateModified": "2019-08-24T14:15:22Z",
  "gameVersionTypeId": 0,
  "gameVersionStatus": 1,
  "gameVersionTypeStatus": 1
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| gameVersionId | integer(int32) | none |
| versionString | string | none |
| jarDownloadUrl | string | none |
| jsonDownloadUrl | string | none |
| approved | boolean | none |
| dateModified | string(date-time) | none |
| gameVersionTypeId | integer(int32) | none |
| gameVersionStatus | GameVersionStatus | 1=Approved, 2=Deleted, 3=New |
| gameVersionTypeStatus | GameVersionTypeStatus | 1=Normal, 2=Deleted |

---

### MinecraftModLoaderIndex

```json
{
  "name": "string",
  "gameVersion": "string",
  "latest": true,
  "recommended": true,
  "dateModified": "2019-08-24T14:15:22Z",
  "type": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| name | string | none |
| gameVersion | string | none |
| latest | boolean | none |
| recommended | boolean | none |
| dateModified | string(date-time) | none |
| type | ModLoaderType | 0=Any, 1=Forge, 2=Cauldron, 3=LiteLoader, 4=Fabric, 5=Quilt, 6=NeoForge |

---

### MinecraftModLoaderVersion

```json
{
  "id": 0,
  "gameVersionId": 0,
  "minecraftGameVersionId": 0,
  "forgeVersion": "string",
  "name": "string",
  "type": 0,
  "downloadUrl": "string",
  "filename": "string",
  "installMethod": 1,
  "latest": true,
  "recommended": true,
  "approved": true,
  "dateModified": "2019-08-24T14:15:22Z",
  "mavenVersionString": "string",
  "versionJson": "string",
  "librariesInstallLocation": "string",
  "minecraftVersion": "string",
  "additionalFilesJson": "string",
  "modLoaderGameVersionId": 0,
  "modLoaderGameVersionTypeId": 0,
  "modLoaderGameVersionStatus": 1,
  "modLoaderGameVersionTypeStatus": 1,
  "mcGameVersionId": 0,
  "mcGameVersionTypeId": 0,
  "mcGameVersionStatus": 1,
  "mcGameVersionTypeStatus": 1,
  "installProfileJson": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| gameVersionId | integer(int32) | none |
| minecraftGameVersionId | integer(int32) | none |
| forgeVersion | string | none |
| name | string | none |
| type | ModLoaderType | 0=Any, 1=Forge, 2=Cauldron, 3=LiteLoader, 4=Fabric, 5=Quilt, 6=NeoForge |
| downloadUrl | string | none |
| filename | string | none |
| installMethod | ModLoaderInstallMethod | 1=ForgeInstaller, 2=ForgeJarInstall, 3=ForgeInstaller_v2, 4=FabricInstaller, 5=QuiltInstaller, 6=NeoForgeInstaller |
| latest | boolean | none |
| recommended | boolean | none |
| approved | boolean | none |
| dateModified | string(date-time) | none |
| mavenVersionString | string | none |
| versionJson | string | none |
| librariesInstallLocation | string | none |
| minecraftVersion | string | none |
| additionalFilesJson | string | none |
| modLoaderGameVersionId | integer(int32) | none |
| modLoaderGameVersionTypeId | integer(int32) | none |
| modLoaderGameVersionStatus | GameVersionStatus | 1=Approved, 2=Deleted, 3=New |
| modLoaderGameVersionTypeStatus | GameVersionTypeStatus | 1=Normal, 2=Deleted |
| mcGameVersionId | integer(int32) | none |
| mcGameVersionTypeId | integer(int32) | none |
| mcGameVersionStatus | GameVersionStatus | 1=Approved, 2=Deleted, 3=New |
| mcGameVersionTypeStatus | GameVersionTypeStatus | 1=Normal, 2=Deleted |
| installProfileJson | string | none |

---

### Mod

```json
{
  "id": 0,
  "gameId": 0,
  "name": "string",
  "slug": "string",
  "links": {
    "websiteUrl": "string",
    "wikiUrl": "string",
    "issuesUrl": "string",
    "sourceUrl": "string"
  },
  "summary": "string",
  "status": 1,
  "downloadCount": 0,
  "isFeatured": true,
  "primaryCategoryId": 0,
  "categories": [ { "...Category..." } ],
  "classId": 0,
  "authors": [ { "...ModAuthor..." } ],
  "logo": { "...ModAsset..." },
  "screenshots": [ { "...ModAsset..." } ],
  "mainFileId": 0,
  "latestFiles": [ { "...File..." } ],
  "latestFilesIndexes": [ { "...FileIndex..." } ],
  "latestEarlyAccessFilesIndexes": [ { "...FileIndex..." } ],
  "dateCreated": "2019-08-24T14:15:22Z",
  "dateModified": "2019-08-24T14:15:22Z",
  "dateReleased": "2019-08-24T14:15:22Z",
  "allowModDistribution": true,
  "gamePopularityRank": 0,
  "isAvailable": true,
  "thumbsUpCount": 0,
  "rating": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | The mod id |
| gameId | integer(int32) | The game id this mod is for |
| name | string | The name of the mod |
| slug | string | The mod slug that would appear in the URL |
| links | ModLinks | Relevant links for the mod such as Issue tracker and Wiki |
| summary | string | Mod summary |
| status | ModStatus | Current mod status |
| downloadCount | integer(int64) | Number of downloads for the mod |
| isFeatured | boolean | Whether the mod is included in the featured mods list |
| primaryCategoryId | integer(int32) | The main category of the mod |
| categories | [Category] | List of categories that this mod is related to |
| classId | integer(int32)\|null | The class id this mod belongs to |
| authors | [ModAuthor] | List of the mod's authors |
| logo | ModAsset | The mod's logo asset |
| screenshots | [ModAsset] | List of screenshots assets |
| mainFileId | integer(int32) | The id of the main file of the mod |
| latestFiles | [File] | List of latest files of the mod |
| latestFilesIndexes | [FileIndex] | List of file related details for the latest files |
| latestEarlyAccessFilesIndexes | [FileIndex] | List of file related details for the latest early access files |
| dateCreated | string(date-time) | The creation date of the mod |
| dateModified | string(date-time) | The last time the mod was modified |
| dateReleased | string(date-time) | The release date of the mod |
| allowModDistribution | boolean\|null | Is mod allowed to be distributed |
| gamePopularityRank | integer(int32) | The mod popularity rank for the game |
| isAvailable | boolean | Is the mod available for search |
| thumbsUpCount | integer(int32) | The mod's thumbs up count |
| rating | number(decimal)\|null | The mod's Rating |

---

### ModAsset

```json
{
  "id": 0,
  "modId": 0,
  "title": "string",
  "description": "string",
  "thumbnailUrl": "string",
  "url": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| modId | integer(int32) | none |
| title | string | none |
| description | string | none |
| thumbnailUrl | string | none |
| url | string | none |

---

### ModAuthor

```json
{
  "id": 0,
  "name": "string",
  "url": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| id | integer(int32) | none |
| name | string | none |
| url | string | none |

---

### ModLinks

```json
{
  "websiteUrl": "string",
  "wikiUrl": "string",
  "issuesUrl": "string",
  "sourceUrl": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| websiteUrl | string | none |
| wikiUrl | string | none |
| issuesUrl | string | none |
| sourceUrl | string | none |

---

### ModLoaderInstallMethod

```
1
```

Possible values: 1=ForgeInstaller, 2=ForgeJarInstall, 3=ForgeInstaller_v2, 4=FabricInstaller, 5=QuiltInstaller, 6=NeoForgeInstaller

---

### ModLoaderType

```
0
```

Possible values: 0=Any, 1=Forge, 2=Cauldron, 3=LiteLoader, 4=Fabric, 5=Quilt, 6=NeoForge

---

### ModsSearchSortField

```
1
```

Possible values: 1=Featured, 2=Popularity, 3=LastUpdated, 4=Name, 5=Author, 6=TotalDownloads, 7=Category, 8=GameVersion, 9=EarlyAccess, 10=FeaturedReleased, 11=ReleasedDate, 12=Rating

---

### ModStatus

```
1
```

Possible values: 1=New, 2=ChangesRequired, 3=UnderSoftReview, 4=Approved, 5=Rejected, 6=ChangesMade, 7=Inactive, 8=Abandoned, 9=Deleted, 10=UnderReview

---

### Pagination

```json
{
  "index": 0,
  "pageSize": 0,
  "resultCount": 0,
  "totalCount": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| index | integer(int32) | A zero based index of the first item that is included in the response |
| pageSize | integer(int32) | The requested number of items to be included in the response |
| resultCount | integer(int32) | The actual number of items that were included in the response |
| totalCount | integer(int64) | The total number of items available by the request |

---

### Search Mods Response

```json
{
  "data": [ { "...Mod..." } ],
  "pagination": { "...Pagination..." }
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | [Mod] | The response data |
| pagination | Pagination | The response pagination information |

---

### SortableGameVersion

```json
{
  "gameVersionName": "string",
  "gameVersionPadded": "string",
  "gameVersion": "string",
  "gameVersionReleaseDate": "2019-08-24T14:15:22Z",
  "gameVersionTypeId": 0
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| gameVersionName | string | Original version name (e.g. 1.5b) |
| gameVersionPadded | string | Used for sorting (e.g. 0000000001.0000000005) |
| gameVersion | string | game version clean name (e.g. 1.5) |
| gameVersionReleaseDate | string(date-time) | Game version release date |
| gameVersionTypeId | integer(int32)\|null | Game version type id |

---

### SortOrder

```
"asc"
```

Possible values: `asc`, `desc`

---

### String Response

```json
{
  "data": "string"
}
```

**Properties**

| Name | Type | Description |
|------|------|-------------|
| data | string | The response data |

---

## CurseForge for Studios Terms of Use

Overwolf Ltd. ("**Overwolf**") makes available the Curseforge platform for user-generated content ("**Curseforge**"), as well as the CurseForge for Studios suite of tools for game developers to integrate in-game support for user-generated content ("**UGC**") in their proprietary game software (the "**Game**"), which may include APIs, plugins and user interfaces (collectively, "**CurseForge for Studios**"). "**You**" or "**Developer**" means anyone who desires to use the CurseForge for Studios Platform.

Please read these Terms of Use (this "**Agreement**") carefully. All use of CurseForge for Studios is subject to this Agreement. By using or downloading CurseForge for Studios or clicking "accept", you signify your assent to both this Agreement and our [Privacy Policy](https://www.overwolf.com/legal/privacy/). If you do not agree to any terms or conditions of this Agreement, please do not use CurseForge for Studios.

1. **CurseForge for Studios Platform** — Subject to the terms and conditions hereof, Overwolf grants Developer a limited, non-exclusive license to use the CurseForge for Studios Platform, including all code made available by Overwolf as part of the CurseForge for Studios Platform, and any accompanying documentation files solely for the purpose of integrating in-game UGC support for Developer's game.

2. **Restrictions** — Developer shall use CurseForge for Studios solely for the Purpose. Developer will not (a) rent, lease, modify, copy, loan, transfer, sublicense, distribute or create derivative works; (b) reverse engineer, decompile, translate, adapt, or disassemble; (c) attempt to disable or circumvent any security mechanism; or (d) remove or obscure any copyright or other notices.

3. **Games and User-Content** — Developer retains sole ownership of the Game, but provides Overwolf and users of Curseforge with a non-exclusive, royalty-free, worldwide license to the name, logo and trademarks of the Game and Developer for the purpose of indicating the availability of the Game and UGC.

4. **Payment** — Overwolf currently provides CurseForge for Studios at no charge. Payment and revenue share obligations may be set forth in a separate written agreement.

5. **The Modding Community** — Overwolf alone will be authorized to manage and moderate the user community and any UGC on the Overwolf platform.

6. **Moderation Privileges** — If Overwolf consents to Developer exercising moderation privileges, Developer must follow agreed guidelines and applicable legal obligations.

7. **Representations and Warranties** — Developer warrants that the Game does not infringe third-party intellectual property rights, contain malicious code, or impose open source obligations on Overwolf.

8. **Indemnification** — Developer shall defend, indemnify and hold harmless Overwolf from any claims arising from the Game, UGC, or Developer's breach of warranties.

9. **Disclaimer; Limitation of Liability** — CURSEFORGE FOR STUDIOS AND ALL DOCUMENTATION ARE PROVIDED "AS-IS". OVERWOLF EXPRESSLY DISCLAIMS ALL WARRANTIES.

10. **Confidentiality** — All non-public information regarding CurseForge for Studios or Documentation is proprietary and confidential information of Overwolf.

11. **Term and Termination** — The Agreement is in effect from the date accepted and continues for an initial term of three years, auto-renewing for subsequent three-year terms.

12. **Miscellaneous** — This Agreement constitutes the entire agreement between the parties.

13. **Dispute Resolution; Governing Law** — This Agreement shall be construed in accordance with the laws of the State of Delaware. Any disputes shall be resolved through binding arbitration in the State of Delaware.
