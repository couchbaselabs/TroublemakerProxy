# MessageInterceptor Plugin

The configuration for this plugin has one root element `Rules` which contains a list of rules to apply to a given message.  The rules will be screened via their `InputCriteria` and their `OutputTransforms` will be applied if applicable.  Refer to the [sample_config.json](sample_config.json) file for layout.

## RuleDirection Key

This determines which direction the rule is applied in.  It is a flags style enum with the following values:

- ToServer (Applied on messages from local and remote)
- ToClient (Applied on messages from remote to local)

As with all configuration components, combining flags is performed via a string with comma separated values (e.g. "ToServer, ToClient")

## Criteria Key

The input criteria is an object which has the following properties:

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| ApplicationFrequency | Determines how often the rule will be applied | Enum | Always, OnlyOnce |
| Type | The type of message that this rule applies to | Enum |Request, Response, Error, AckRequest, AckResponse |
| Profile | The profile to match when determining application | String ||
| Flags | The flags to match when determining application | Flags | Compressed, Urgent, NoReply, MoreComing |
| Number | The message number to apply this rule to | UInt64 ||
| Properties | The properties to match when determining application | Dictionary<String, String> ||

## OutputTransforms Key

This key contains an array of output transforms, each of which has the `Portion` key.  The value of this key determines the additional properties that it can accept.

- Type

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| Type | The new type for the message | Enum | Request, Response, Error, AckRequest, AckResponse |
- Properties

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| Op | The way to apply the value contained in `Properties`.  `Replace` erases all properties then adds, `Add` adds or replaces without erasing first, `Remove` removes the keys in the passed properties | Enum | Add, Remove, Replace |
| Properties | The properties to apply to the message | Dictionary<String, String> ||
- Body

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| Content | The new body for the message | String ||
- MessageNumber

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| Op | The way to apply the value contained in `Number`.  Either replacing, or applying a math operation. | Enum | Add, Divide, Multiply, Replace, Subtract  |
| Number | The number to apply to the message | UInt64 ||
- Flags

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| Op | The way to apply the value contained in `Flags`. | Enum | Add, Remove, Replace |
| Flags | The flags to apply to the message (note: Adding or removing the compression flag will result in a compressed or uncompressed message) | Flags | Compressed, Urgent, NoReply, MoreComing |
- Profile

| Key | Purpose | Type | Valid Values |
| --- | ------- | ---- | ------------ |
| Profile | The new profile for the message | String ||
