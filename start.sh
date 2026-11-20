#!/bin/sh
set -eu

SECRET_FILE="/etc/secrets/oracle-wallet.zip.b64"
WALLET_DIR="/tmp/oracle-wallet"
WALLET_ZIP="/tmp/oracle-wallet.zip"

if [ ! -f "$SECRET_FILE" ]; then
    echo "ERROR: No se encontró el Wallet secreto de Oracle."
    exit 1
fi

mkdir -p "$WALLET_DIR"
base64 -d "$SECRET_FILE" > "$WALLET_ZIP"
unzip -oq "$WALLET_ZIP" -d "$WALLET_DIR"
rm -f "$WALLET_ZIP"

printf 'WALLET_LOCATION = (SOURCE = (METHOD = file) (METHOD_DATA = (DIRECTORY="%s")))\nSSL_SERVER_DN_MATCH=yes\n' "$WALLET_DIR" > "$WALLET_DIR/sqlnet.ora"

export TNS_ADMIN="$WALLET_DIR"
exec dotnet ReposteriaApi.dll
