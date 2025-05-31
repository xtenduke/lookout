## Lookout
Update docker containers from a SQS queue


Lookout will listen to the queue for messages, read ImageTag and ImageName from the message and compare it to running containers on your machine.


If the tag doesn't match the message, lookout will:
- Download the new image
- Copy the container config to a new container with the new image
- Kill the old container
- Start the new one (if fails, restart the old one)

#### Message body
- ImageName: the name of your image
- ImageTag: the tag
- DeployTimeSeconds: how long lookout should expect for the image do download, old one to shut down and new one to spin up
```
{
  "ImageName": "redis",
  "ImageTag": "7",
  "DeployTimeSeconds": "30"
}
```

### Why?
'Simple' deploys from CI


### How?

#### Create a SQS Queue

#### Create AWS credentials for CI to send messages:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "VisualEditor0",
            "Effect": "Allow",
            "Action": "sqs:SendMessage",
            "Resource": "<your-sqs-queue-arn>"
        }
    ]
}
```


#### Create AWS credentials for your server to recieve messages:
```
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "VisualEditor0",
            "Effect": "Allow",
            "Action": [
                "sqs:DeleteMessage",
                "sqs:ReceiveMessage"
            ],
            "Resource": "<your queue arn>"
        }
   
```


#### Run lookout on your server with config

run.sh
```
docker pull ghcr.io/xtenduke/lookout:latest
docker run -d \
  --env-file .env \
  -v /var/run/docker.sock:/var/run/docker.sock \
  --restart always \
  ghcr.io/xtenduke/lookout:latest
```

.env
```
LogLevel=debug
SqsQueueUrl=https://sqs.ap-southeast-2.amazonaws.com/<your-acct-id>/<your-queue-name>

(or provide your own aws auth)
AWS_ACCESS_KEY_ID=<server key id>
AWS_SECRET_ACCESS_KEY=<server secret>
AWS_REGION=<queue region>

(optional)
RegistryUsername=<private registry username>
RegistryPassword=<private registry password>
```

Publish a message from your CI or elsewhere to inform lookout of an update (example github ci)
```
- name: Send SQS Message backend
  env:
    AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
    AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
    AWS_REGION: ${{ secrets.AWS_REGION }}
    SQS_QUEUE_URL: ${{ secrets.SQS_QUEUE_URL }}
    IMAGE_NAME: ${{ env.IMAGE_NAME }}
    IMAGE_TAG: ${{ steps.vars.outputs.tag }}
  run: |
    aws sqs send-message \
      --queue-url "$SQS_QUEUE_URL" \
      -message-body "{\"ImageName\":\"ghcr.io/${{ env.IMAGE_NAME }}\",\"ImageTag\":\"${{ steps.vars.outputs.tag }}\",\"DeployTimeSeconds\":\"30\"}"
```


### Caveats / WIP
- Supports single listener only, messages are deleted/consumed when containers are updated
- Container filtering isn't that good at the moment, you should clean up old versions of your containers before running this
- This container needs to bind the docker socket
- No feedback to CI if deploy has succeeded
- Message parsing is fragile 
