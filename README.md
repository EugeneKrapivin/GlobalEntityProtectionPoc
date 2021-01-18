# GlobalEntityProtectionPoc

This is a poc of a systemic feature allowing protection from alteration/deletion without creating tight coupling between the services serving those entities.
The solution is eventually consistent, there are consistency issues that can arise from such a solution.

## Required configurations

```json
{
  "Storage": {
    "ConnectionString": "azure table storage connection string"
  },
  "Kafka": {
    "Brokers": [
      "broker-host:port",
      "broker-host:port",
      "broker-host:port"
    ] 
  } 
}
```

Kafka cluster that allows auto creation of topic

**OR**

create 2 topics
* unprotection-requests
* protection-requests
