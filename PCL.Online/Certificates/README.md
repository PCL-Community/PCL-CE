# N Cloud 证书

将生产服务器使用的公开证书复制到本目录，并命名为：

```text
n-cloud.cer
```

证书可以是 DER 或 PEM 编码的 X.509 公开证书。构建时该证书会作为资源嵌入
`PCL.Online.dll`，N Cloud 请求只接受与该证书 SHA-256 指纹完全一致、且主机名匹配的服务端。

mTLS 客户端证书需要在构建时提供：

```text
n-cloud-client.pfx
```

`n-cloud-client.pfx` 包含客户端私钥，必须保持未加密空密码，并且已被 `.gitignore` 忽略，不要提交到仓库。
GitHub Actions 会从仓库 Secrets 还原它：

```text
N_CLOUD_SERVER_CERT_BASE64
N_CLOUD_CLIENT_PFX_BASE64
```

PowerShell 生成 Secret 内容：

```powershell
$serverCert = [Convert]::ToBase64String([IO.File]::ReadAllBytes("PCL.Online/Certificates/n-cloud.cer"))
gh secret set N_CLOUD_SERVER_CERT_BASE64 --body $serverCert

$clientPfx = [Convert]::ToBase64String([IO.File]::ReadAllBytes("PCL.Online/Certificates/n-cloud-client.pfx"))
gh secret set N_CLOUD_CLIENT_PFX_BASE64 --body $clientPfx
```

Linux/macOS 生成 Secret 内容：

```bash
gh secret set N_CLOUD_SERVER_CERT_BASE64 \
  --body "$(base64 < PCL.Online/Certificates/n-cloud.cer | tr -d '\r\n')"

gh secret set N_CLOUD_CLIENT_PFX_BASE64 \
  --body "$(base64 < PCL.Online/Certificates/n-cloud-client.pfx | tr -d '\r\n')"
```

Debug 模式也可以暂时不放证书，改用环境变量：

```powershell
$env:PCL_ONLINE_SERVER_CERT_SHA256="证书的 SHA-256 指纹"
```

正式发布必须嵌入 `n-cloud.cer` 和 `n-cloud-client.pfx`。更换服务端证书、客户端证书或证书指纹白名单后，
需要同步更新 Secrets 并重新发布客户端。
