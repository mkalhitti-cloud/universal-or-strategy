# Copyright 2026 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     https://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
import logging
import os
import re
import socket
import threading
import time
from typing import Any

import posthog
import sentry_sdk
import vertexai
from dotenv import load_dotenv
from google.adk.artifacts import GcsArtifactService, InMemoryArtifactService
from google.cloud import logging as google_cloud_logging
from vertexai.agent_engines.templates.adk import AdkApp

from app.agent import app as adk_app
from app.app_utils.telemetry import setup_telemetry
from app.app_utils.typing import Feedback

# Load environment variables from .env file at runtime
load_dotenv()


class AgentEngineApp(AdkApp):
    def set_up(self) -> None:
        """Initialize the agent engine app with logging and telemetry."""
        logging.basicConfig(level=logging.INFO)
        vertexai.init()
        setup_telemetry()
        super().set_up()
        logging_client = google_cloud_logging.Client()
        self.logger = logging_client.logger(__name__)
        if gemini_location:
            os.environ["GOOGLE_CLOUD_LOCATION"] = gemini_location

        # Start the IPC listener for NinjaTrader logs
        threading.Thread(target=self._start_ipc_listener, daemon=True).start()

    def _start_ipc_listener(self) -> None:
        """Connects to NinjaTrader IPC server and routes logs to telemetry."""
        host = "127.0.0.1"
        port = 5001
        log_pattern = re.compile(r"\[TRACE:(?P<id>\d+)\]\[(?P<module>.*?)\]\[(?P<level>.*?)\] (?P<message>.*)")

        while True:
            try:
                with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                    s.connect((host, port))
                    logging.info(f"Connected to NinjaTrader IPC at {host}:{port}")
                    
                    buffer = ""
                    while True:
                        data = s.recv(4096).decode("utf-8")
                        if not data:
                            break
                        
                        buffer += data
                        while "\n" in buffer:
                            line, buffer = buffer.split("\n", 1)
                            line = line.strip()
                            if not line:
                                continue
                                
                            # Local echo
                            logging.info(f"[NT8] {line}")
                            
                            # Telemetry Routing
                            match = log_pattern.match(line)
                            if match:
                                parts = match.groupdict()
                                level = parts["level"].upper()
                                module = parts["module"]
                                message = parts["message"]
                                
                                # Sentry for Errors/Warnings
                                if level in ["ERROR", "CRITICAL"]:
                                    sentry_sdk.capture_message(
                                        f"[{module}] {message}", 
                                        level="error",
                                        extra={"nt8_line": line}
                                    )
                                elif level == "WARNING":
                                    sentry_sdk.capture_message(
                                        f"[{module}] {message}", 
                                        level="warning",
                                        extra={"nt8_line": line}
                                    )
                                    
                                # PostHog for Domain Events (SIMA, Reaper, etc)
                                if "SIMA" in module or "REAPER" in module or "STRATEGY" in module:
                                    posthog.capture(
                                        distinct_id="nt8_kernel",
                                        event=f"nt8_{module.lower().replace('.', '_')}",
                                        properties={
                                            "message": message,
                                            "level": level,
                                            "trace_id": parts["id"]
                                        }
                                    )
            except (ConnectionRefusedError, socket.error):
                # Silent retry - NT8 might not be running yet
                time.sleep(5)
            except Exception as e:
                logging.error(f"IPC Listener Error: {e}")
                time.sleep(5)

    def register_feedback(self, feedback: dict[str, Any]) -> None:
        """Collect and log feedback."""
        feedback_obj = Feedback.model_validate(feedback)
        self.logger.log_struct(feedback_obj.model_dump(), severity="INFO")

        # Product Analytics Loophole
        if os.environ.get("POSTHOG_API_KEY"):
            import posthog
            posthog.capture(
                distinct_id=feedback.get("session_id", "anonymous_agent"),
                event="agent_feedback_received",
                properties=feedback_obj.model_dump()
            )

    def register_operations(self) -> dict[str, list[str]]:
        """Registers the operations of the Agent."""
        operations = super().register_operations()
        operations[""] = [*operations.get("", []), "register_feedback"]
        return operations


gemini_location = os.environ.get("GOOGLE_CLOUD_LOCATION")
logs_bucket_name = os.environ.get("LOGS_BUCKET_NAME")
agent_runtime = AgentEngineApp(
    app=adk_app,
    artifact_service_builder=lambda: (
        GcsArtifactService(bucket_name=logs_bucket_name)
        if logs_bucket_name
        else InMemoryArtifactService()
    ),
)
