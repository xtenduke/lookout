#!/bin/bash

set -a
source .env
set +a

set -ex

aws sqs send-message --queue-url $SqsQueueUrl --message-body '{"ImageName":"redis","ImageTag":"7", "DeployTimeSeconds": "30"}'