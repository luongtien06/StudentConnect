with open('Models/Assistant/SocialTools.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target = """            var connectItem = new ConnectPost
            {
                UserID = currentUserId,
                CategoryID = targetCatId,
                Title = cleanTitle,
                Description = (description ?? "").Trim(),
                CreatedAt = DateTime.Now
            };"""

replacement = """            var connectItem = new ConnectPost
            {
                UserID = currentUserId,
                CategoryID = targetCatId,
                Title = cleanTitle,
                Description = (description ?? "").Trim(),
                MainImage = StudentConnect.Helpers.DefaultImagePlaceholders.CoverPath,
                CreatedAt = DateTime.Now
            };"""

text_norm = text.replace('\r\n', '\n')
target_norm = target.replace('\r\n', '\n')
replacement_norm = replacement.replace('\r\n', '\n')

if target_norm in text_norm:
    text_norm = text_norm.replace(target_norm, replacement_norm, 1)
    with open('Models/Assistant/SocialTools.cs', 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text_norm)
    print('Successfully added MainImage to ConnectPost!')
else:
    print('Target not found')
