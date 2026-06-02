#!/usr/bin/env bash
set -e
PORT=$(k3d kubeconfig get taskflow | grep server | grep -oE '[0-9]{4,5}')
echo "API server port: $PORT"
kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:$PORT"
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true
