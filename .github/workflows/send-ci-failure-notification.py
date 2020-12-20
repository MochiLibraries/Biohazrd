#!/usr/bin/env python3
import os
import sys
from urllib import request

import gha

def get_environment_variable(name):
    ret = os.getenv(name)

    if ret is None or ret == '':
        gha.print_error(f"Missing required parameter '{name}'")

    return ret

webhook_url = get_environment_variable('webhook_url')
github_repo = get_environment_variable('github_repo')
github_workflow_name = get_environment_variable('github_workflow_name')
github_run_number = get_environment_variable('github_run_number')
gha.fail_if_errors()

card_data = f'''{{
  "type": "message",
  "attachments": [
    {{
      "contentType": "application/vnd.microsoft.card.adaptive",
      "contentUrl": null,
      "content": {{
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.2",
          "body": [
            {{
              "type": "TextBlock",
              "text": "{github_repo}: {github_workflow_name} Failed!",
              "wrap": true,
              "weight": "bolder"
            }},
            {{
              "type": "ActionSet",
              "actions": [
                {{
                  "type": "Action.OpenUrl",
                  "title": "Show Run",
                  "url": "https://github.com/{github_repo}/actions/runs/{github_run_number}"
                }}
              ]
            }}
          ]
        }}
    }}
  ]
}}'''
card_data = card_data.encode('utf-8')

webhook_request = request.Request(webhook_url, data=card_data, headers={'Content-Type': 'application/json'})
webhook_response = request.urlopen(webhook_request)

response_string = webhook_response.read().decode('utf-8')

if response_string != '1' or webhook_response.status != 200:
    print(f"Response code: {webhook_response.status}")
    print(f"Response body: {response_string}")
    sys.exit(1)
