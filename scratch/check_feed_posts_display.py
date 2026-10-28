import requests
from bs4 import BeautifulSoup

session = requests.Session()
# Login as student
login_data = {
    'Email': 'student@tdmu.edu.vn',
    'Password': 'User@123',
    'RememberMe': 'true'
}
resp = session.post('http://localhost:59174/User/Login', data=login_data, allow_redirects=True)
feed_resp = session.get('http://localhost:59174/Home/Index')
soup = BeautifulSoup(feed_resp.text, 'html.parser')

print("Feed status:", feed_resp.status_code)
# Find post cards and their time ago text
cards = soup.select('.post-card, .card, [data-post-id]')
print(f"Total cards/elements: {len(cards)}")

# Let's search for time ago strings in the page
for elem in soup.find_all(attrs={'class': lambda c: c and ('time' in c or 'date' in c)}):
    print(elem.get('class'), ":", elem.get_text(strip=True))

