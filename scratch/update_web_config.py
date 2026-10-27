with open("Web.config", "rb") as f:
    data = f.read()

target = b'\t\t\t\t<requestLimits maxAllowedContentLength="104857600" />\r\n\t\t\t</requestFiltering>'
rep = b'\t\t\t\t<requestLimits maxAllowedContentLength="104857600" />\r\n\t\t\t\t<hiddenSegments>\r\n\t\t\t\t\t<add segment="App_Data" />\r\n\t\t\t\t\t<add segment="Config" />\r\n\t\t\t\t</hiddenSegments>\r\n\t\t\t</requestFiltering>'

if target in data:
    data = data.replace(target, rep, 1)
    with open("Web.config", "wb") as f:
        f.write(data)
    print("SUCCESS")
else:
    print("TARGET NOT FOUND")
