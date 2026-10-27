with open("Web.config", "rb") as f:
    data = f.read()

target = b'\t\t\t\t<hiddenSegments>\r\n\t\t\t\t\t<add segment="App_Data" />\r\n\t\t\t\t\t<add segment="Config" />\r\n\t\t\t\t</hiddenSegments>'
rep = b'\t\t\t\t<hiddenSegments>\r\n\t\t\t\t\t<remove segment="App_Data" />\r\n\t\t\t\t\t<add segment="App_Data" />\r\n\t\t\t\t\t<add segment="Config" />\r\n\t\t\t\t</hiddenSegments>'

if target in data:
    data = data.replace(target, rep, 1)
    with open("Web.config", "wb") as f:
        f.write(data)
    print("SUCCESS: Updated Web.config with remove segment")
else:
    print("TARGET NOT FOUND")
