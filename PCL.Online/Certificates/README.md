# N Cloud 固定证书

将 Ubuntu Nginx 使用的公开证书复制到本目录，并命名为：

```text
n-cloud.cer
```

证书可以是 DER 或 PEM 编码的 X.509 公开证书，不要放入私钥。构建时该证书会作为资源嵌入
`PCL.Online.dll`，N Cloud 请求只接受与该证书 SHA-256 指纹完全一致、且主机名匹配的服务端。

Debug 模式也可以暂时不放证书，改用环境变量：

```powershell
$env:PCL_ONLINE_SERVER_CERT_SHA256="证书的 SHA-256 指纹"
```

正式发布必须嵌入 `n-cloud.cer`。更换服务端证书后，需要更新此文件并重新发布客户端。
