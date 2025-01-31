using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CardDeck : MonoBehaviour
{
    public ClueCard[] characterCards;
    public ClueCard[] weaponCards;
    public ClueCard[] roomCards;

    private ClueCard correctCharacter;
    private ClueCard correctWeapon;
    private ClueCard correctRoom;
    private Queue<ClueCard> remainingCards;

    public int TotalCards { get => characterCards.Length + weaponCards.Length + roomCards.Length; }

    private void Awake()
    {
        remainingCards = new Queue<ClueCard>();
    }

    public void Initialize()
    {
        remainingCards.Clear();
        // set aside the winning cards
        correctCharacter = characterCards[Random.Range(0, characterCards.Length)];
        correctWeapon = weaponCards[Random.Range(0, weaponCards.Length)];
        correctRoom = roomCards[Random.Range(0, roomCards.Length)];
        // add all other cards to one list
        List<ClueCard> tempList = characterCards.Concat(weaponCards).Concat(roomCards).ToList();
        // shuffle
        tempList.Sort((x, y) => Random.Range(-1, 1));
        foreach (ClueCard card in tempList)
        {
            if (card != correctCharacter && card != correctWeapon && card != correctRoom)
            {
                remainingCards.Enqueue(card);
            }
        }
    }

    public List<ClueCard> GetCards(int numCards)
    {
        List<ClueCard> retList = new List<ClueCard>();
        for (int i = 0; i < numCards; i++)
        {
            if (remainingCards.Count > 0)
            {
                retList.Add(remainingCards.Dequeue());
            }
        }
        return retList;
    }

    public List<ClueCard> GetCorrectCards()
    {
        return new List<ClueCard>() { correctCharacter, correctRoom, correctWeapon };
    }

    public bool IsCorrectGuess(CharacterType guessCharacter, WeaponType guessWeapon, RoomType guessRoom)
    {
        bool status = true;
        if (correctCharacter.TryGetCharacterType(out CharacterType character))
        {
            status &= (character == guessCharacter);
        }
        if (correctWeapon.TryGetWeaponType(out WeaponType weapon))
        {
            status &= (weapon == guessWeapon);
        }
        if (correctRoom.TryGetRoomType(out RoomType room))
        {
            status &= (room == guessRoom);
        }
        return status;
    }

    public static bool GetCluesFromCards(List<ClueCard> cards, out List<CharacterType> characters, out List<WeaponType> weapons, out List<RoomType> rooms)
    {
        bool ret = true;
        
        characters = new List<CharacterType>();
        weapons = new List<WeaponType>();
        rooms = new List<RoomType>();
        
        foreach (ClueCard card in cards)
        {
            if (card.TryGetCharacterType(out CharacterType character))
            {
                characters.Add(character);
            }
            else if (card.TryGetWeaponType(out WeaponType weapon))
            {
                weapons.Add(weapon);
            }
            else if (card.TryGetRoomType(out RoomType room))
            {
                rooms.Add(room);
            }
            else
            {
                ret = false;
            }
        }
        return ret;
    }

    public List<ClueCard> GetCardsFromClues(List<CharacterType> characters, List<WeaponType> weapons, List<RoomType> rooms)
    {
        List<ClueCard> cardList = new List<ClueCard>();
        foreach (CharacterType character in characters)
        {
            cardList.Add(characterCards.First(x => x.name == character.ToString()));
        }
        foreach (WeaponType weapon in weapons)
        {
            cardList.Add(weaponCards.First(x => x.name == weapon.ToString()));
        }
        foreach (RoomType room in rooms)
        {
            cardList.Add(roomCards.First(x => x.name == room.ToString()));
        }
        return cardList;
    }

    public bool TryGetCard(CharacterType character, out ClueCard charCard)
    {
        foreach (ClueCard card in characterCards)
        {
            if (card.TryGetCharacterType(out CharacterType outChar) && outChar == character)
            {
                charCard = card;
                return true;
            }
        }
        charCard = null;
        return false;
    }
    public bool TryGetCard(WeaponType weapon, out ClueCard weaponCard)
    {
        foreach (ClueCard card in weaponCards)
        {
            if (card.TryGetWeaponType(out WeaponType outWeapon) && outWeapon == weapon)
            {
                weaponCard = card;
                return true;
            }
        }
        weaponCard = null;
        return false;
    }
    public bool TryGetCard(RoomType room, out ClueCard roomCard)
    {
        foreach (ClueCard card in roomCards)
        {
            if (card.TryGetRoomType(out RoomType outRoom) && outRoom == room)
            {
                roomCard = card;
                return true;
            }
        }
        roomCard = null;
        return false;
    }
}